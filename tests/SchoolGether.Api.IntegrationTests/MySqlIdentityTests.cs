using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Contracts.Identity;
using SchoolGether.Contracts.Institutions;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class MySqlIdentityTests
{
    [MySqlFact]
    public async Task Identity_WithMySqlMigrations_IsolatesInstitutionsAndRevokesSessions()
    {
        await using var factory = new IdentityFactory(
            mySqlConnectionString: Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"));
        await factory.SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            Assert.Equal("MySql.EntityFrameworkCore", db.Database.ProviderName);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        Assert.NotEqual(factory.InstitutionA, factory.InstitutionB);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var platform = await Login(client, "platform@example.test");
        Authenticate(client, platform);
        using var createInstitution = await client.PostAsJsonAsync("/api/v1/institutions", new CreateInstitutionRequest("Instituição C"));
        Assert.Equal(HttpStatusCode.Created, createInstitution.StatusCode);
        var institution = (await createInstitution.Content.ReadFromJsonAsync<InstitutionResponse>())!;
        using var createAdmin = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "admin-c@example.test", IdentityFactory.Password, "Admin C", "InstitutionAdmin", institution.Id));
        Assert.Equal(HttpStatusCode.Created, createAdmin.StatusCode);

        var adminC = await Login(client, "admin-c@example.test");
        Authenticate(client, adminC);
        var me = (await client.GetFromJsonAsync<AccountResponse>("/api/v1/identity/me"))!;
        Assert.Equal("InstitutionAdmin", me.Role);
        Assert.Equal(institution.Id, me.InstitutionId);
        using var createStudent = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "student-c@example.test", IdentityFactory.Password, "Aluno C", "Student", null));
        Assert.Equal(HttpStatusCode.Created, createStudent.StatusCode);
        var membersC = (await client.GetFromJsonAsync<MemberPageResponse>("/api/v1/identity/users"))!;
        Assert.Equal(2, membersC.Total);
        Assert.All(membersC.Items, member => Assert.Equal(institution.Id, member.InstitutionId));
        Assert.Contains(membersC.Items, member => member.Email == "student-c@example.test");

        using var crossInstitution = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "blocked@example.test", IdentityFactory.Password, "Bloqueado", "Student", factory.InstitutionA));
        Assert.Equal(HttpStatusCode.Forbidden, crossInstitution.StatusCode);

        var adminA = await Login(client, "admin-a@example.test");
        Authenticate(client, adminA);
        var membersA = (await client.GetFromJsonAsync<MemberPageResponse>("/api/v1/identity/users"))!;
        Assert.Equal(3, membersA.Total);
        Assert.All(membersA.Items, member => Assert.Equal(factory.InstitutionA, member.InstitutionId));
        Assert.DoesNotContain(membersA.Items, member => member.Email == "blocked@example.test");

        foreach (var email in new[] { "teacher-a@example.test", "student-c@example.test" })
        {
            var token = await Login(client, email);
            Authenticate(client, token);
            Assert.Equal(token.Account, await client.GetFromJsonAsync<AccountResponse>("/api/v1/identity/me"));
            using var forbidden = await client.GetAsync("/api/v1/identity/users");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        using var refresh = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(adminA.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var next = (await refresh.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(adminA.RefreshToken, next.RefreshToken);
        using var logout = await client.PostAsJsonAsync("/api/v1/identity/logout", new RefreshRequest(next.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var revoked = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(next.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    private static async Task<TokenResponse> Login(HttpClient client, string email)
    {
        client.DefaultRequestHeaders.Authorization = null;
        using var response = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest(email, IdentityFactory.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private static void Authenticate(HttpClient client, TokenResponse token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
}

public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL")))
            Skip = "Executar scripts/Test-MySqlIdentity.ps1 para preparar o MySQL temporário.";
    }
}
