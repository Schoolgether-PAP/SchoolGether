using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SchoolGether.Contracts.Diagnostics;

namespace SchoolGether.Api.IntegrationTests;

public sealed class ApiBaseTests
{
    private static HttpClient CreateClient(ApiFactory factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    [Fact]
    public async Task Liveness_RemainsHealthyWhenDatabaseCheckThrows()
    {
        await using var factory = new ApiFactory(databaseThrows: true);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new HealthResponse("Healthy"), await response.Content.ReadFromJsonAsync<HealthResponse>());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Readiness_ReturnsHealthyWhenDatabaseIsReady()
    {
        await using var factory = new ApiFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/api/v1/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new HealthResponse("Healthy"), await response.Content.ReadFromJsonAsync<HealthResponse>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Readiness_FailureReturns503WithoutPrivateDetails(bool throws)
    {
        await using var factory = new ApiFactory(databaseReady: false, databaseThrows: throws);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/api/v1/health/ready");

        var problem = await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        Assert.Contains("indisponível", problem.GetProperty("title").GetString());
        Assert.DoesNotContain("private-test-marker", problem.ToString());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task UnhandledException_ReturnsGenericProblemDetails(string environment)
    {
        await using var factory = new ApiFactory(environment);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/api/v1/test/failure");

        var problem = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        Assert.Contains("erro inesperado", problem.GetProperty("title").GetString());
        Assert.DoesNotContain("private-test-marker", problem.ToString());
        Assert.DoesNotContain("InvalidOperationException", problem.ToString());
    }

    [Theory]
    [InlineData("/api/v2/health")]
    [InlineData("/weatherforecast")]
    [InlineData("/api/v1/missing")]
    public async Task UnknownRoute_Returns404ProblemDetails(string path)
    {
        await using var factory = new ApiFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(path);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WrongMethod_Returns405ProblemDetails()
    {
        await using var factory = new ApiFactory();
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync("/api/v1/health", new { });

        await AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task InvalidInput_ReturnsValidationProblemDetails()
    {
        await using var factory = new ApiFactory();
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync("/api/v1/test/validation", new { value = 0 });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Value", out _));
    }

    [Theory]
    [InlineData("http://localhost:5227")]
    [InlineData("https://localhost:7256")]
    public async Task Cors_AllowsConfiguredSitePreflight(string origin)
    {
        await using var factory = new ApiFactory();
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("Development", "https://unlisted.example")]
    [InlineData("Production", "http://localhost:5227")]
    public async Task Cors_DoesNotAllowUnconfiguredOrigins(string environment, string origin)
    {
        await using var factory = new ApiFactory(environment);
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Development_PublishesSwaggerAndVersionedOpenApi()
    {
        await using var factory = new ApiFactory();
        using var client = CreateClient(factory);

        using var swagger = await client.GetAsync("/swagger/index.html");
        using var document = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        Assert.Contains("swagger-ui-bundle.js", await swagger.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        using var json = JsonDocument.Parse(await document.Content.ReadAsStringAsync());
        var paths = json.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/health", out _));
        Assert.True(paths.TryGetProperty("/api/v1/health/ready", out _));
        Assert.False(paths.TryGetProperty("/weatherforecast", out _));
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Production_DoesNotPublishDevelopmentDocumentation(string path)
    {
        await using var factory = new ApiFactory("Production");
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(path);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement.Clone();
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("instance").GetString()));
        return problem;
    }
}
