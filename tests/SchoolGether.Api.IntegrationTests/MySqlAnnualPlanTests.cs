using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Contracts.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class MySqlAnnualPlanTests
{
    [MySqlFact]
    public async Task Upgrade_preserves_monthly_contract_and_new_signup_uses_teacher_places()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true,
            mySqlConnectionString: Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"),
            migrationTarget: "20261020225655_AcademicClasses");
        await factory.SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            var adminId = await db.Users.Where(x => x.Email == "admin-a@example.test").Select(x => x.Id).SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO demonstration_subscription (institution_id, status, plan_id, plan_name_key, catalog_version, monthly_price, currency, account_limit, used_accounts, created_at, created_by, row_version) VALUES ({factory.InstitutionA}, 'Demonstration', 'essential', 'PlanEssential', '2026-10-14-v1', 59, 'EUR', 100, 3, UTC_TIMESTAMP(6), {adminId}, 1)");
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var legacy = await db.Subscriptions.IgnoreQueryFilters().SingleAsync();
            Assert.Equal(59m, legacy.Price);
            Assert.Equal("Monthly", legacy.BillingPeriod);
            Assert.Equal("Accounts", legacy.CapacityUnit);
            Assert.Equal(100, legacy.AccountLimit);
            Assert.Equal(3, legacy.UsedAccounts);
            Assert.Equal("2026-10-14-v1", legacy.CatalogVersion);
        }
        using var client = factory.CreateClient();
        using var signup = await client.PostAsJsonAsync(OnboardingTests.Route, OnboardingTests.Request());
        Assert.Equal(HttpStatusCode.Created, signup.StatusCode);
        var institutionId = (await signup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("institutionId").GetInt32();
        using var login = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest("new-owner@example.test", IdentityFactory.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
        foreach (var role in new[] { "Student", "Teacher" })
        {
            using var created = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
                $"annual-{role}@example.test", IdentityFactory.Password, "Utilizador", role, null));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
        using var verify = factory.Services.CreateScope();
        var actual = verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var current = await actual.Subscriptions.IgnoreQueryFilters().SingleAsync(x => x.InstitutionId == institutionId);
        Assert.Equal(1000m, current.Price);
        Assert.Equal("Annual", current.BillingPeriod);
        Assert.Equal("Teachers", current.CapacityUnit);
        Assert.Equal(1, current.UsedAccounts);
        Assert.Equal(3, await actual.Members.IgnoreQueryFilters().CountAsync(x => x.InstitutionId == institutionId));
        Assert.Equal(3, (await actual.Subscriptions.IgnoreQueryFilters().SingleAsync(x => x.InstitutionId == factory.InstitutionA)).UsedAccounts);
    }
}
