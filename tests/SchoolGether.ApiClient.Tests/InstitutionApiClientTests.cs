using System.Net;
using System.Text.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.ApiClient.Institutions;
using SchoolGether.Contracts.Billing;
using SchoolGether.Contracts.Identity;
using SchoolGether.Contracts.Institutions;

namespace SchoolGether.ApiClient.Tests;

public sealed class InstitutionApiClientTests
{
    [Fact]
    public async Task InstitutionalRoutesUseSessionAndTypedPayloads()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (request, _) => Task.FromResult(request.Path switch
        {
            "/api/v1/institutions/current/setup" => SessionTestFixture.Json(new InstitutionSetupResponse(2, "Escola", "School", null, "2026/2027", true, 3, "2026/2027")),
            "/api/v1/institutions/current/subscription" => SessionTestFixture.Json(new InstitutionSubscriptionResponse("None", null, null, 1)),
            "/api/v1/identity/users?page=2&pageSize=12" => SessionTestFixture.Json(new MemberPageResponse([], 0, 2, 12)),
            "/api/v1/identity/users" => SessionTestFixture.Json(SessionTestFixture.Profile),
            _ => throw new InvalidOperationException(request.Path)
        });
        var client = new InstitutionApiClient(fixture.Http, fixture.Session);
        Assert.Equal(2, (await client.GetSetupAsync()).InstitutionId);
        Assert.Equal(3, (await client.SaveSetupAsync(new("Escola", "School", null, "2026/2027", 2))).RowVersion);
        Assert.Equal("None", (await client.GetSubscriptionAsync()).Status);
        Assert.Equal(2, (await client.GetMembersAsync(2, 12)).Page);
        Assert.Equal(7, (await client.CreateUserAsync(new("student@example.test", "Example-Only42!", "Aluno", "Student", null))).Id);
        var sent = fixture.Requests.Skip(2).ToArray();
        Assert.All(sent, x => Assert.Equal("access-1", x.Bearer));
        var save = Assert.Single(sent, x => x.Method == "PUT");
        using var body = JsonDocument.Parse(save.Body!);
        Assert.Equal(2, body.RootElement.GetProperty("rowVersion").GetInt64());
        Assert.Null(fixture.Http.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task UnauthorizedRefreshesAndLateResponsesCannotCompleteAfterLogout()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (request, _) => Task.FromResult(request.Path.EndsWith("refresh")
            ? SessionTestFixture.Json(fixture.Tokens("access-2", SessionTestFixture.SecondRefresh))
            : request.Bearer == "access-1" ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : SessionTestFixture.Json(new InstitutionSubscriptionResponse("None", null, null, 1)));
        var client = new InstitutionApiClient(fixture.Http, fixture.Session);
        Assert.Equal(1, (await client.GetSubscriptionAsync()).UsedAccounts);
        Assert.Equal(new[] { "access-1", "access-2" }, fixture.Requests.Where(x => x.Path.EndsWith("subscription")).Select(x => x.Bearer));
        var started = SessionTestFixture.NewSignal();
        var release = SessionTestFixture.NewSignal();
        fixture.Respond = async (request, _) =>
        {
            if (request.Path.EndsWith("logout")) return new(HttpStatusCode.NoContent);
            started.SetResult(); await release.Task;
            return SessionTestFixture.Json(new InstitutionSubscriptionResponse("None", null, null, 1));
        };
        var pending = client.GetSubscriptionAsync();
        await started.Task;
        await fixture.Session.SignOutAsync(); release.SetResult();
        await Assert.ThrowsAsync<SessionExpiredException>(() => pending);
    }
}
