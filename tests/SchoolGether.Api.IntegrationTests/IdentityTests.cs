using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SchoolGether.Application.Identity;
using SchoolGether.Contracts.Identity;
using SchoolGether.Contracts.Institutions;
using SchoolGether.Domain.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class IdentityTests
{
    private static HttpClient Client(IdentityFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task<TokenResponse> Login(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest(email, IdentityFactory.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }
    private static void Authenticate(HttpClient client, TokenResponse token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

    [Fact]
    public async Task Login_IssuesJwtAndMeRequiresAuthentication()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = Client(factory);
        using var anonymous = await client.GetAsync("/api/v1/identity/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Contains("Bearer", anonymous.Headers.WwwAuthenticate.ToString());
        var token = await Login(client, "student-a@example.test");
        Assert.Equal("Student", token.Account.Role);
        Assert.Equal(factory.InstitutionA, token.Account.InstitutionId);
        Assert.Equal(3, token.AccessToken.Split('.').Length);
        Authenticate(client, token);
        Assert.Equal(token.Account, await client.GetFromJsonAsync<AccountResponse>("/api/v1/identity/me"));
    }

    [Fact]
    public async Task Refresh_RotatesAndReplayRevokesReplacement()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = Client(factory);
        var original = await Login(client, "student-a@example.test");
        using var refresh = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(original.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var next = (await refresh.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(original.RefreshToken, next.RefreshToken);
        Assert.Equal(original.RefreshTokenExpiresAt, next.RefreshTokenExpiresAt);
        Assert.Equal(DateTimeKind.Utc, next.RefreshTokenExpiresAt.Kind);
        using var replay = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(original.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        using var revoked = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(next.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        using var scope = factory.Services.CreateScope();
        var sessions = await scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Sessions.ToListAsync();
        Assert.DoesNotContain(sessions, x => x.TokenHash == original.RefreshToken || x.TokenHash == next.RefreshToken);
        Assert.All(sessions, x => Assert.NotNull(x.RevokedAt));
    }

    [Fact]
    public async Task Logout_PreventsRefresh()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        var token = await Login(client, "student-a@example.test");
        using var logout = await client.PostAsJsonAsync("/api/v1/identity/logout", new RefreshRequest(token.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var refresh = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(token.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task InstitutionAdmin_CannotListOrCreateAccountsInAnotherInstitution()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        Authenticate(client, await Login(client, "admin-a@example.test"));
        client.DefaultRequestHeaders.Add("X-Institution-Id", factory.InstitutionB.ToString());
        var page = (await client.GetFromJsonAsync<MemberPageResponse>("/api/v1/identity/users?institutionId=" + factory.InstitutionB))!;
        Assert.Equal(3, page.Total);
        Assert.All(page.Items, account => Assert.Equal(factory.InstitutionA, account.InstitutionId));
        Assert.DoesNotContain(page.Items, account => account.Email.Contains("-b@"));
        using var forbidden = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "intruder@example.test", IdentityFactory.Password, "Intruso", "Teacher", factory.InstitutionB));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("application/problem+json", forbidden.Content.Headers.ContentType?.MediaType);
        using var promote = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "new-admin@example.test", IdentityFactory.Password, "Admin", "InstitutionAdmin", factory.InstitutionA));
        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
        using var allowed = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "new-teacher-a@example.test", IdentityFactory.Password, "Docente", "Teacher", null));
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        Assert.Equal(factory.InstitutionA, (await allowed.Content.ReadFromJsonAsync<AccountResponse>())!.InstitutionId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.False(await db.Users.AnyAsync(x => x.Email == "intruder@example.test"));
    }

    [Theory]
    [InlineData("student-a@example.test")]
    [InlineData("student-b@example.test")]
    [InlineData("teacher-a@example.test")]
    public async Task Member_CannotManageAccountsOrInstitutions(string email)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        Authenticate(client, await Login(client, email));
        using var users = await client.GetAsync("/api/v1/identity/users");
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);
        using var institution = await client.PostAsJsonAsync("/api/v1/institutions", new CreateInstitutionRequest("Outra"));
        Assert.Equal(HttpStatusCode.Forbidden, institution.StatusCode);
        using var user = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "blocked@example.test", IdentityFactory.Password, "Bloqueado", "Student", factory.InstitutionA));
        Assert.Equal(HttpStatusCode.Forbidden, user.StatusCode);
    }

    [Fact]
    public async Task PlatformAdmin_CreatesInstitutionAndItsFirstAdmin()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        var token = await Login(client, "platform@example.test"); Authenticate(client, token);
        Assert.Null(token.Account.InstitutionId);
        using var institution = await client.PostAsJsonAsync("/api/v1/institutions", new CreateInstitutionRequest("Instituição C"));
        Assert.Equal(HttpStatusCode.Created, institution.StatusCode);
        var created = (await institution.Content.ReadFromJsonAsync<InstitutionResponse>())!;
        using var admin = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "admin-c@example.test", IdentityFactory.Password, "Admin C", "InstitutionAdmin", created.Id));
        Assert.Equal(HttpStatusCode.Created, admin.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        var login = await Login(client, "admin-c@example.test"); Authenticate(client, login);
        Assert.Equal(created.Id, login.Account.InstitutionId);
        var page = (await client.GetFromJsonAsync<MemberPageResponse>("/api/v1/identity/users"))!;
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task FailedLogins_LockAccountAndDoNotRevealItsExistence()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        string? message = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest("student-a@example.test", "WrongPassword"));
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            using var problem = JsonDocument.Parse(await failed.Content.ReadAsStringAsync());
            message = problem.RootElement.GetProperty("title").GetString();
        }
        using var locked = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest("student-a@example.test", IdentityFactory.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        using var missing = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest("missing@example.test", IdentityFactory.Password));
        using var missingProblem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        Assert.Equal(message, missingProblem.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    public async Task InvalidJwt_IsRejected(string failure)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        var login = await Login(client, "student-a@example.test");
        using var rsa = RSA.Create(2048);
        if (failure != "signature") rsa.ImportFromPem(factory.PrivateKey);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(failure == "issuer" ? "other" : "SchoolGether",
            failure == "audience" ? "other" : "SchoolGether.Clients",
            [new Claim("sub", login.Account.Id.ToString()), new Claim("role", "Student"),
             new Claim("institution_id", factory.InstitutionA.ToString())],
            now.AddHours(-1), failure == "expired" ? now.AddMinutes(-2) : now.AddMinutes(10),
            new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        using var response = await client.GetAsync("/api/v1/identity/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingTenant_HidesMembersAndDeniesWriting()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Empty(await db.Members.ToListAsync());
        var existing = await db.Members.IgnoreQueryFilters().FirstAsync();
        db.Entry(existing).Property(x => x.Name).CurrentValue = "Tentativa";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task IdentityRateLimit_Returns429ProblemDetails()
    {
        await using var factory = new IdentityFactory(); using var client = Client(factory);
        // Validação falha antes de consultar a base; o limitador conta os pedidos.
        for (var attempt = 0; attempt < 10; attempt++)
            using (var response = await client.PostAsJsonAsync("/api/v1/identity/login", new { }))
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var rejected = await client.PostAsJsonAsync("/api/v1/identity/login", new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(rejected.Headers.RetryAfter);
    }

    [Fact]
    public async Task Refresh_ExpiredSessionIsRejected()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        var token = await Login(client, "student-a@example.test");
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Sessions
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddDays(-1)));
        using var response = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(token.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_DeletedInstitutionIsRejected()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        var token = await Login(client, "student-a@example.test");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            (await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionA)).Delete();
            await db.SaveChangesAsync();
        }
        using var response = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(token.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConditionalRotation_RejectsConsumedSnapshotAndRevokesFamily()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        var token = await Login(client, "student-a@example.test");
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ISessionStore>();
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token.RefreshToken)));
        var snapshot = (await store.FindAsync(hash, default))!;
        using var refresh = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(token.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.False(await store.RotateAsync(snapshot, snapshot with { Hash = new string('A', 64) }, DateTime.UtcNow, default));
        var next = (await refresh.Content.ReadFromJsonAsync<TokenResponse>())!;
        using var rejected = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(next.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    [Fact]
    public async Task AnonymousRegistration_DoesNotExist()
    {
        await using var factory = new IdentityFactory(); using var client = Client(factory);
        using var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantWriteGuard_RejectsOtherInstitutionInAllSaveOverloads()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        await using var db = new SchoolGetherDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<SchoolGetherDbContext>>(),
            new TestAccount(factory.InstitutionA));
        var other = await db.Members.IgnoreQueryFilters().FirstAsync(x => x.InstitutionId == factory.InstitutionB);
        db.Entry(other).Property(x => x.Name).CurrentValue = "Não autorizado";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(true));
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        db.Members.Add(new InstitutionMember(999, factory.InstitutionB, "Não autorizado", AccountRole.Student, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DuplicateAccount_ReturnsConflictAndInvalidRoleReturnsValidationError()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        Authenticate(client, await Login(client, "admin-a@example.test"));
        using var duplicate = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "STUDENT-A@example.test", IdentityFactory.Password, "Aluno", "Student", null));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var role = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "invalid@example.test", IdentityFactory.Password, "Aluno", "PlatformAdmin", null));
        Assert.Equal(HttpStatusCode.BadRequest, role.StatusCode);
    }

    [Fact]
    public async Task Swagger_DocumentsBearerAndCreatedResponses()
    {
        await using var factory = new IdentityFactory(); using var client = Client(factory);
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty("Bearer").GetProperty("scheme").GetString());
        var paths = root.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/v1/identity/me").GetProperty("get").GetProperty("security").GetArrayLength() > 0);
        Assert.True(paths.GetProperty("/api/v1/identity/users").GetProperty("post").GetProperty("responses").TryGetProperty("201", out _));
    }

    private sealed class TestAccount(int institutionId) : ICurrentAccount
    {
        public int? UserId => 1;
        public int? InstitutionId => institutionId;
        public string? Role => "InstitutionAdmin";
    }
}
