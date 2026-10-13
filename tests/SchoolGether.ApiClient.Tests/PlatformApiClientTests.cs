using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.ApiClient.Platform;
using SchoolGether.Contracts.Identity;
using SchoolGether.Contracts.Platform;

namespace SchoolGether.ApiClient.Tests;

public sealed class PlatformApiClientTests
{
    [Fact]
    public async Task ListsUseServerPaginationAndEncodedFilters()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (request, _) => Task.FromResult(request.Path.StartsWith("/api/v1/platform/audit")
            ? SessionTestFixture.Json(new PagedResponse<AuditResponse>([], 0, 2, 15))
            : request.Path.StartsWith("/api/v1/platform/admins")
                ? SessionTestFixture.Json(new PagedResponse<PlatformAdminResponse>([], 0, 3, 10))
                : SessionTestFixture.Json(new PagedResponse<PlatformInstitutionResponse>([], 0, 4, 12)));
        var client = new PlatformApiClient(fixture.Http, fixture.Session);

        var admins = await client.ListAdminsAsync(3, 10);
        var institutions = await client.ListInstitutionsAsync(4, 12, "Escola & Norte", true);
        var audit = await client.ListAuditAsync(2, 15, "ação & alvo", 17);

        Assert.Equal(3, admins.Page);
        Assert.Equal(4, institutions.Page);
        Assert.Equal(2, audit.Page);
        Assert.Contains(fixture.Requests, x => x.Path == "/api/v1/platform/admins?page=3&pageSize=10");
        Assert.Contains(fixture.Requests, x => x.Path == "/api/v1/platform/institutions?page=4&pageSize=12&search=Escola%20%26%20Norte&isSuspended=true");
        Assert.Contains(fixture.Requests, x => x.Path == "/api/v1/platform/audit?page=2&pageSize=15&search=a%C3%A7%C3%A3o%20%26%20alvo&institutionId=17");
        Assert.All(fixture.Requests.Where(x => x.Path.StartsWith("/api/v1/platform")), x =>
        {
            Assert.Equal("Bearer", x.Scheme);
            Assert.Equal("access-1", x.Bearer);
        });
        Assert.Null(fixture.Http.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task AllDetailAndMutationRoutesUseTypedPayloads()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        var institution = new PlatformInstitutionResponse(9, "Escola", false, null, DateTime.UtcNow, 2, 4);
        var admin = new PlatformAdminResponse(8, "admin@test", "Admin", false, true, [], 2);
        var preference = new DashboardPreferenceResponse(1, 2, [new DashboardWidget("institutions-trend", "wide", true)]);
        fixture.Respond = (request, _) => Task.FromResult(request.Path switch
        {
            "/api/v1/platform/context" => SessionTestFixture.Json(new PlatformContextResponse(7, true, [], 1)),
            "/api/v1/platform/admins" or "/api/v1/platform/admins/8" => SessionTestFixture.Json(admin),
            "/api/v1/platform/institutions/9" when request.Method == "GET" => SessionTestFixture.Json(new PlatformInstitutionDetailResponse(institution, [])),
            "/api/v1/platform/institutions" or "/api/v1/platform/institutions/9" or "/api/v1/platform/institutions/9/suspension" => SessionTestFixture.Json(institution),
            "/api/v1/platform/institutions/9/administrators" => SessionTestFixture.Json(SessionTestFixture.Profile),
            "/api/v1/platform/dashboard" => SessionTestFixture.Json(new PlatformDashboardResponse(1, 1, 0, 2, [], [])),
            "/api/v1/platform/dashboard/preferences" => SessionTestFixture.Json(preference),
            _ => throw new InvalidOperationException(request.Path)
        });
        var client = new PlatformApiClient(fixture.Http, fixture.Session);

        Assert.True((await client.GetContextAsync()).IsMaster);
        Assert.Equal(8, (await client.CreateAdminAsync(new CreatePlatformAdminRequest { Email = "admin@test", Password = "long-password", Name = "Admin" })).Id);
        Assert.Equal(8, (await client.UpdateAdminAsync(8, new UpdatePlatformAdminRequest { Name = "Admin", IsActive = true, RowVersion = 1, Reason = "test" })).Id);
        Assert.Equal(9, (await client.GetInstitutionAsync(9)).Institution.Id);
        Assert.Equal(9, (await client.CreateInstitutionAsync(new CreatePlatformInstitutionRequest { Name = "Escola", Reason = "test" })).Id);
        Assert.Equal(9, (await client.UpdateInstitutionAsync(9, new UpdatePlatformInstitutionRequest { Name = "Escola", RowVersion = 3, Reason = "test" })).Id);
        Assert.Equal(9, (await client.SetInstitutionSuspensionAsync(9, new SetInstitutionSuspensionRequest { IsSuspended = true, RowVersion = 4, Reason = "test" })).Id);
        Assert.Equal(7, (await client.CreateInstitutionAdminAsync(9, new CreateInstitutionAdminRequest { Email = "student@example.test", Password = "long-password", Name = "Aluno", Reason = "test" })).Id);
        Assert.Equal(1, (await client.GetDashboardAsync()).InstitutionsTotal);
        Assert.Equal(1, (await client.GetDashboardPreferencesAsync()).SchemaVersion);
        Assert.Equal(2, (await client.SaveDashboardPreferencesAsync(new SaveDashboardPreferenceRequest { SchemaVersion = 1, RowVersion = 1, Widgets = preference.Widgets })).RowVersion);

        var requests = fixture.Requests.Where(x => x.Path.StartsWith("/api/v1/platform")).ToArray();
        Assert.Contains(requests, x => x.Method == "POST" && x.Path == "/api/v1/platform/admins" && HasJson(x, "password", "long-password"));
        Assert.Contains(requests, x => x.Method == "PUT" && x.Path == "/api/v1/platform/admins/8" && HasJson(x, "rowVersion", 1));
        Assert.Contains(requests, x => x.Method == "POST" && x.Path == "/api/v1/platform/institutions" && HasJson(x, "name", "Escola"));
        Assert.Contains(requests, x => x.Method == "PUT" && x.Path == "/api/v1/platform/institutions/9" && HasJson(x, "rowVersion", 3));
        Assert.Contains(requests, x => x.Method == "PUT" && x.Path == "/api/v1/platform/institutions/9/suspension" && HasJson(x, "isSuspended", true));
        Assert.Contains(requests, x => x.Method == "POST" && x.Path == "/api/v1/platform/institutions/9/administrators" && HasJson(x, "email", "student@example.test"));
        Assert.Contains(requests, x => x.Method == "PUT" && x.Path == "/api/v1/platform/dashboard/preferences" && HasJson(x, "rowVersion", 1));
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("")]
    [InlineData("{\"title\":123,\"traceId\":[]}")]
    public async Task MalformedProblemPreservesStatusWithoutEchoingBody(string body)
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(body) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/problem+json") } }
        });
        var client = new PlatformApiClient(fixture.Http, fixture.Session);

        var error = await Assert.ThrowsAsync<IdentityApiException>(() => client.GetContextAsync());

        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
        if (body.Length > 0) Assert.DoesNotContain(body, error.Message);
        Assert.Equal(SessionTestFixture.Profile, fixture.Session.Account);
    }

    [Fact]
    public async Task EmptySuccessfulResponseDoesNotExposeBodyAndKeepsStatus()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        });
        var client = new PlatformApiClient(fixture.Http, fixture.Session);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetContextAsync());

        Assert.Equal(HttpStatusCode.OK, error.StatusCode);
        Assert.Equal(SessionTestFixture.Profile, fixture.Session.Account);
    }

    [Fact]
    public async Task ProblemDetailsKeepsSafeTitleAndTraceId()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"title\":\"Sem permissão\",\"traceId\":\"trace-4\"}")
            { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/problem+json") } }
        });
        var client = new PlatformApiClient(fixture.Http, fixture.Session);

        var error = await Assert.ThrowsAsync<IdentityApiException>(() => client.GetContextAsync());

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Equal("Sem permissão", error.Message);
        Assert.Equal("trace-4", error.TraceId);
    }

    [Fact]
    public async Task UnauthorizedEndpointRefreshesAndRetriesWithNewRequestToken()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (request, _) => Task.FromResult(request.Path switch
        {
            "/api/v1/platform/context" when request.Bearer == "access-1" => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            "/api/v1/platform/context" => SessionTestFixture.Json(new PlatformContextResponse(7, true, [], 1)),
            "/api/v1/identity/refresh" => SessionTestFixture.Json(fixture.Tokens("access-2", SessionTestFixture.SecondRefresh)),
            _ => throw new InvalidOperationException(request.Path)
        });
        var client = new PlatformApiClient(fixture.Http, fixture.Session);

        Assert.True((await client.GetContextAsync()).IsMaster);

        Assert.Equal(new[] { "access-1", "access-2" }, fixture.Requests
            .Where(x => x.Path == "/api/v1/platform/context").Select(x => x.Bearer));
        Assert.Single(fixture.Requests, x => x.Path == "/api/v1/identity/refresh");
        Assert.Null(fixture.Http.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task LatePlatformResponseCannotCompleteAfterLogout()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        var started = SessionTestFixture.NewSignal();
        var release = SessionTestFixture.NewSignal();
        fixture.Respond = async (request, _) =>
        {
            if (request.Path == "/api/v1/identity/logout") return new HttpResponseMessage(HttpStatusCode.NoContent);
            started.SetResult();
            await release.Task;
            return SessionTestFixture.Json(new PlatformContextResponse(7, true, [], 1));
        };
        var client = new PlatformApiClient(fixture.Http, fixture.Session);
        var pending = client.GetContextAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await fixture.Session.SignOutAsync();
        release.SetResult();

        await Assert.ThrowsAsync<SessionExpiredException>(() => pending);
    }

    [Fact]
    public async Task CancellationStopsPlatformRequest()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        using var cancellation = new CancellationTokenSource();
        var started = SessionTestFixture.NewSignal();
        fixture.Respond = async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        };
        var client = new PlatformApiClient(fixture.Http, fixture.Session);
        var pending = client.GetContextAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    private static bool HasJson<T>(SessionRequest request, string property, T value)
    {
        using var document = JsonDocument.Parse(request.Body!);
        return document.RootElement.TryGetProperty(property, out var actual)
            && actual.ToString() == value?.ToString();
    }
}
