using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class MySqlOnboardingTests
{
    [MySqlFact]
    public async Task Concurrent_normalized_email_creates_exactly_one_complete_institution()
    {
        await using var factory = new IdentityFactory(mySqlConnectionString: Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"), onboardingEnabled: true);
        await factory.SeedAsync();
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync(OnboardingTests.Route, OnboardingTests.Request("concurrent@example.test")),
            second.PostAsJsonAsync(OnboardingTests.Route, OnboardingTests.Request("CONCURRENT@EXAMPLE.TEST")));
        try
        {
            Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, responses.Select(x => x.StatusCode).Order().ToArray());
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            Assert.Equal("MySql.EntityFrameworkCore", db.Database.ProviderName);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(7, await db.Users.CountAsync());
            Assert.Equal(3, await db.Institutions.CountAsync());
            Assert.Equal(6, await db.Members.IgnoreQueryFilters().CountAsync());
            var owner = await db.Users.SingleAsync(x => x.NormalizedEmail == "CONCURRENT@EXAMPLE.TEST");
            var member = await db.Members.IgnoreQueryFilters().SingleAsync(x => x.UserId == owner.Id);
            var subscription = await db.Subscriptions.IgnoreQueryFilters().SingleAsync();
            var profile = await db.InstitutionProfiles.IgnoreQueryFilters().SingleAsync();
            Assert.Equal(member.InstitutionId, subscription.InstitutionId);
            Assert.Equal(member.InstitutionId, profile.InstitutionId);
            Assert.Equal(1, subscription.UsedAccounts);
            Assert.Equal(owner.Id, subscription.CreatedBy);
            Assert.Equal(owner.Id, profile.CreatedBy);
            Assert.Equal(owner.Id, owner.CreatedBy);
            Assert.False(owner.IsPlatformAdmin);
            Assert.Equal("InstitutionAdmin", member.Role.ToString());
            Assert.Single(await db.AuditEntries.ToListAsync());
            Assert.Empty(await db.Sessions.ToListAsync());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
}
