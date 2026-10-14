using System.Net;
using System.Text.Json;
using SchoolGether.ApiClient.Billing;
using SchoolGether.ApiClient.Identity;
using SchoolGether.ApiClient.Onboarding;
using SchoolGether.Contracts.Billing;
using SchoolGether.Contracts.Onboarding;

namespace SchoolGether.ApiClient.Tests;

public sealed class OnboardingApiClientTests
{
    private static readonly RegisterInstitutionRequest Registration = new("essential", "v1", "Responsável", "owner@example.test", "Example-Only42!", "Escola", "School", "Lisboa", true);

    [Fact]
    public async Task PublicRoutesUseTypedCatalogAndRegistrationWithoutAuthorization()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (request, _) => Task.FromResult(request.Path == "/api/v1/billing/plans"
            ? SessionTestFixture.Json(new PlanCatalogResponse("v1", true, [new("essential", "Essencial", 59, "EUR", 100)]))
            : SessionTestFixture.Json(new RegistrationResponse(12, 13, Registration.Email)));
        var catalog = await new PlanApiClient(fixture.Http).GetCatalogAsync();
        var created = await new OnboardingApiClient(fixture.Http).RegisterAsync(Registration);
        Assert.Equal(100, Assert.Single(catalog.Plans).AccountLimit);
        Assert.Equal(12, created.InstitutionId);
        var sent = Assert.Single(fixture.Requests, x => x.Path == "/api/v1/onboarding/institutions");
        Assert.Equal("POST", sent.Method);
        Assert.Null(sent.Scheme);
        Assert.Null(Assert.Single(fixture.Requests, x => x.Path == "/api/v1/billing/plans").Scheme);
        using var body = JsonDocument.Parse(sent.Body!);
        Assert.Equal("v1", body.RootElement.GetProperty("catalogVersion").GetString());
        Assert.Equal("School", body.RootElement.GetProperty("institutionType").GetString());
        Assert.Equal(9, body.RootElement.EnumerateObject().Count());
    }

    [Theory]
    [InlineData(400)] [InlineData(409)] [InlineData(429)] [InlineData(503)]
    public async Task RegistrationPreservesErrorsWithoutRetry(int status)
    {
        using var fixture = new SessionTestFixture();
        fixture.Respond = (_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        var error = await Assert.ThrowsAsync<IdentityApiException>(() => new OnboardingApiClient(fixture.Http).RegisterAsync(Registration));
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.Single(fixture.Requests);
    }

    [Fact]
    public async Task LostRegistrationResponseIsNotRetried()
    {
        using var fixture = new SessionTestFixture();
        fixture.Respond = (_, _) => throw new HttpRequestException("connection lost");
        await Assert.ThrowsAsync<HttpRequestException>(() => new OnboardingApiClient(fixture.Http).RegisterAsync(Registration));
        Assert.Single(fixture.Requests);
    }
}
