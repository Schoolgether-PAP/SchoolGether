using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class PlatformDashboardTests
{
    [Fact]
    public async Task Dashboard_UsesRealCountsAndSixUtcMonths_IncludingEmptyAndPartialMonths()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var a = await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionA);
            var b = await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionB);
            db.Entry(a).Property(x => x.CreatedAt).CurrentValue = month.AddMonths(-5);
            db.Entry(b).Property(x => x.CreatedAt).CurrentValue = month.AddMonths(-6);
            await db.SaveChangesAsync();
        }
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        var result = await master.GetFromJsonAsync<JsonElement>("/api/v1/platform/dashboard");
        Assert.Equal(2, result.GetProperty("institutionsTotal").GetInt32());
        Assert.Equal(2, result.GetProperty("activeInstitutions").GetInt32());
        Assert.Equal(0, result.GetProperty("suspendedInstitutions").GetInt32());
        Assert.Equal(6, result.GetProperty("accountsTotal").GetInt32());
        var trend = result.GetProperty("institutionsTrend").EnumerateArray().ToArray();
        Assert.Equal(6, trend.Length); Assert.Equal(1, trend[0].GetProperty("count").GetInt32());
        Assert.All(trend.Skip(1), x => Assert.Equal(0, x.GetProperty("count").GetInt32()));
        Assert.Contains("parcial", trend[^1].GetProperty("label").GetString());
        using var suspend = await master.PutAsJsonAsync($"{PlatformInstitutionTests.Root}/{factory.InstitutionA}/suspension", new { IsSuspended = true, RowVersion = 1, Reason = "Revisão" });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        var updated = await master.GetFromJsonAsync<JsonElement>("/api/v1/platform/dashboard");
        Assert.Equal(1, updated.GetProperty("activeInstitutions").GetInt32());
        Assert.Equal(1, updated.GetProperty("suspendedInstitutions").GetInt32());
        Assert.Contains(updated.GetProperty("recentActions").EnumerateArray(), x => x.GetProperty("action").GetString() == "institution.suspension");
    }

    [Fact]
    public async Task RevokedPermissions_OmitDataAndSavedWidgetsImmediatelyForSameJwt()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        var created = await PlatformAdminTests.Created(master, "dashboard@example.test", "institutions.read", "audit.read");
        using var admin = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(admin, "dashboard@example.test");
        using var saved = await admin.PutAsJsonAsync(DashboardPreferenceTests.Root, DashboardPreferenceTests.Layout(0, "institutions-trend"));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var before = await admin.GetFromJsonAsync<JsonElement>("/api/v1/platform/dashboard");
        Assert.Equal(2, before.GetProperty("institutionsTotal").GetInt32());
        using var revoke = await master.PutAsJsonAsync("/api/v1/platform/admins/" + created.GetProperty("id").GetInt32(), new {
            Name = "Revogado", IsActive = true, Permissions = Array.Empty<string>(), RowVersion = 1, Reason = "Revogar acesso" });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        var after = await admin.GetFromJsonAsync<JsonElement>("/api/v1/platform/dashboard");
        foreach (var property in new[] { "institutionsTotal", "activeInstitutions", "suspendedInstitutions", "accountsTotal" })
            Assert.Equal(JsonValueKind.Null, after.GetProperty(property).ValueKind);
        Assert.Empty(after.GetProperty("institutionsTrend").EnumerateArray());
        Assert.Empty(after.GetProperty("recentActions").EnumerateArray());
        var preference = await admin.GetFromJsonAsync<JsonElement>(DashboardPreferenceTests.Root);
        Assert.Empty(preference.GetProperty("widgets").EnumerateArray());
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "admin-a@example.test");
        using var denied = await school.GetAsync("/api/v1/platform/dashboard"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
