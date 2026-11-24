using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Contracts.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class TeacherCapacityTests
{
    [Fact]
    public async Task Students_do_not_consume_teacher_places_even_when_teacher_quota_is_full()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        using var signup = await client.PostAsJsonAsync(OnboardingTests.Route, OnboardingTests.Request());
        Assert.Equal(HttpStatusCode.Created, signup.StatusCode);
        var institutionId = (await signup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("institutionId").GetInt32();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            await db.Subscriptions.IgnoreQueryFilters().Where(s => s.InstitutionId == institutionId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountLimit, 1));
        }
        using var login = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest("new-owner@example.test", IdentityFactory.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
        async Task<HttpResponseMessage> Create(string email, string role) => await client.PostAsJsonAsync("/api/v1/identity/users",
            new CreateUserRequest(email, IdentityFactory.Password, "Pessoa de teste", role, null));

        // Reservation happens before identity creation: a duplicate must roll it back.
        using var duplicateOwner = await Create("new-owner@example.test", "Teacher");
        Assert.Equal(HttpStatusCode.Conflict, duplicateOwner.StatusCode);
        using var teacher = await Create("teacher-place@example.test", "Teacher");
        Assert.Equal(HttpStatusCode.Created, teacher.StatusCode);
        using var denied = await Create("teacher-excess@example.test", "Teacher");
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        using var student = await Create("student-included@example.test", "Student");
        Assert.Equal(HttpStatusCode.Created, student.StatusCode);
        using var duplicate = await Create("student-included@example.test", "Teacher");
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var subscriptionResponse = await client.GetAsync("/api/v1/institutions/current/subscription");
        Assert.Equal(HttpStatusCode.OK, subscriptionResponse.StatusCode);
        var subscription = await subscriptionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, subscription.GetProperty("usedAccounts").GetInt32());
        Assert.Equal("Annual", subscription.GetProperty("plan").GetProperty("billingPeriod").GetString());
        Assert.Equal("Teachers", subscription.GetProperty("plan").GetProperty("capacityUnit").GetString());
        using var verify = factory.Services.CreateScope();
        var actual = verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Equal(3, await actual.Members.IgnoreQueryFilters().CountAsync(x => x.InstitutionId == institutionId));
        Assert.False(await actual.Users.AnyAsync(x => x.Email == "teacher-excess@example.test"));
    }
}
