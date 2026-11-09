using System.Net;
using System.Text.Json;

namespace SchoolGether.Api.IntegrationTests;

public sealed class AnnualPlanCatalogTests
{
    [Fact]
    public async Task New_catalog_identifies_annual_teacher_seats_without_monthly_price()
    {
        await using var factory = new ApiFactory(databaseThrows: true);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/api/v1/billing/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("2026-11-09-v1", root.GetProperty("version").GetString());
        var plans = root.GetProperty("plans").EnumerateArray().ToArray();
        Assert.Equal(3, plans.Length);
        Assert.Equal(new decimal[] { 1000, 2500, 4000 }, plans.Select(p => p.GetProperty("price").GetDecimal()));
        Assert.Equal(new[] { 20, 50, 100 }, plans.Select(p => p.GetProperty("accountLimit").GetInt32()));
        Assert.Equal(new[] { "Essencial", "Profissional", "Institucional" }, plans.Select(p => p.GetProperty("name").GetString()));
        Assert.All(plans, plan => {
            Assert.Equal("Annual", plan.GetProperty("billingPeriod").GetString());
            Assert.Equal("Teachers", plan.GetProperty("capacityUnit").GetString());
            Assert.False(plan.TryGetProperty("monthlyPrice", out _));
        });
    }
}
