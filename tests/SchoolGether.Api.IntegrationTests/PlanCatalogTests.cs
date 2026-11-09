using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SchoolGether.Application.Billing;
using SchoolGether.Domain.Billing;

namespace SchoolGether.Api.IntegrationTests;

public sealed class PlanCatalogTests
{
    [Fact]
    public void Catalog_has_confirmed_prices_and_capacities()
    {
        var plans = new PlanCatalog().GetPlans();

        Assert.Collection(plans,
            plan => AssertPlan(plan, "essential", "PlanEssential", 1000m, 20),
            plan => AssertPlan(plan, "evolution", "PlanProfessional", 2500m, 50),
            plan => AssertPlan(plan, "campus", "PlanInstitutional", 4000m, 100));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("ESSENTIAL")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_plan_is_rejected(string? planId)
    {
        var exception = Assert.Throws<BillingException>(() =>
            new PlanCatalog().RequirePlan(planId!, "2026-11-09-v1"));

        Assert.Equal("UnknownPlan", exception.Code);
    }

    [Theory]
    [InlineData("2026-10-13-v1")]
    [InlineData("2026-10-14-v1")]
    [InlineData("2026-10-14-V1")]
    [InlineData("")]
    [InlineData(null)]
    public void Outdated_catalog_requires_review(string? version)
    {
        var exception = Assert.Throws<BillingException>(() =>
            new PlanCatalog().RequirePlan("essential", version!));

        Assert.Equal("OutdatedCatalog", exception.Code);
    }

    [Fact]
    public void Current_plan_returns_server_owned_price_and_capacity()
    {
        var plan = new PlanCatalog().RequirePlan("evolution", "2026-11-09-v1");

        AssertPlan(plan, "evolution", "PlanProfessional", 2500m, 50);
    }

    [Fact]
    public void Catalog_cannot_be_changed_by_consumers()
    {
        var catalog = new PlanCatalog();
        var plans = Assert.IsAssignableFrom<IList<SubscriptionPlan>>(catalog.GetPlans());

        Assert.Throws<NotSupportedException>(() => plans.Clear());
        Assert.Equal(3, catalog.GetPlans().Count);
        Assert.Equal(1000m, catalog.RequirePlan("essential", "2026-11-09-v1").Price);
    }

    [Fact]
    public async Task Public_catalog_does_not_require_database_or_authentication()
    {
        await using var factory = new ApiFactory(databaseThrows: true);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/api/v1/billing/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var catalog = json.RootElement;
        Assert.Equal("2026-11-09-v1", catalog.GetProperty("version").GetString());
        Assert.False(catalog.GetProperty("registrationEnabled").GetBoolean());
        Assert.Equal(new[] { "plans", "registrationEnabled", "version" },
            catalog.EnumerateObject().Select(x => x.Name).Order().ToArray());
        Assert.Collection(catalog.GetProperty("plans").EnumerateArray(),
            plan => AssertPublicPlan(plan, "essential", "Essencial", 1000m, 20),
            plan => AssertPublicPlan(plan, "evolution", "Profissional", 2500m, 50),
            plan => AssertPublicPlan(plan, "campus", "Institucional", 4000m, 100));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Public_catalog_reports_server_registration_switch(bool enabled)
    {
        await using var baseFactory = new ApiFactory(databaseThrows: true);
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Onboarding:Enabled"] = enabled.ToString() })));
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/api/v1/billing/plans?registrationEnabled=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(enabled, json.RootElement.GetProperty("registrationEnabled").GetBoolean());
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static void AssertPlan(SubscriptionPlan plan, string id, string nameKey, decimal price, int limit)
    {
        Assert.NotNull(plan);
        Assert.Equal(id, plan.Id);
        Assert.Equal(nameKey, plan.NameKey);
        Assert.Equal(price, plan.Price);
        Assert.Equal("EUR", plan.Currency);
        Assert.Equal(limit, plan.AccountLimit);
    }

    private static void AssertPublicPlan(JsonElement plan, string id, string name, decimal price, int limit)
    {
        Assert.Equal(id, plan.GetProperty("id").GetString());
        Assert.Equal(name, plan.GetProperty("name").GetString());
        Assert.Equal(price, plan.GetProperty("price").GetDecimal());
        Assert.Equal("EUR", plan.GetProperty("currency").GetString());
        Assert.Equal(limit, plan.GetProperty("accountLimit").GetInt32());
        Assert.Equal(new[] { "accountLimit", "billingPeriod", "capacityUnit", "currency", "id", "name", "price" },
            plan.EnumerateObject().Select(x => x.Name).Order().ToArray());
    }
}
