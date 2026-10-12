using System.Net;
using System.Text.Json;
using SchoolGether.ApiClient.Identity;

namespace SchoolGether.ApiClient.Tests;

public sealed class IdentitySessionTests
{
    // Catches trusting the embedded token account or omitting the authenticated /me request.
    [Fact]
    public async Task SignInLoadsProfileFromApi()
    {
        using var fixture = new SessionTestFixture();
        var published = new List<string?>();
        fixture.Session.Changed += () => published.Add(fixture.Session.Account?.Name);

        var account = await fixture.Session.SignInAsync(SessionTestFixture.Credentials);

        Assert.Equal("Aluno", account.Name);
        Assert.Equal(SessionTestFixture.Profile, fixture.Session.Account);
        Assert.Equal(SessionTestFixture.FirstRefresh, await fixture.Store.ReadAsync(default));
        var profile = Assert.Single(fixture.Requests, request => request.Path.EndsWith("/me"));
        Assert.Equal("Bearer", profile.Scheme);
        Assert.Equal("access-1", profile.Bearer);
        Assert.Contains("Aluno", published);
    }

    // Catches keeping a previous user's profile/token when a new login is rejected.
    [Fact]
    public async Task RejectedLoginDoesNotRetainAccount()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<SessionExpiredException>(() => fixture.Session.SignInAsync(SessionTestFixture.Credentials));

        Assert.Null(fixture.Session.Account);
        Assert.Null(await fixture.Store.ReadAsync(default));
    }

    // Catches refreshing the same rotating token twice or using the expired access token.
    [Fact]
    public async Task ExpiredConcurrentRequestsRotateOnce()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        var refreshStarted = SessionTestFixture.NewSignal();
        var releaseRefresh = SessionTestFixture.NewSignal();
        fixture.Respond = async (request, _) =>
        {
            if (request.Path.EndsWith("/refresh"))
            {
                refreshStarted.TrySetResult();
                await releaseRefresh.Task;
                return SessionTestFixture.Json(fixture.Tokens("access-2", SessionTestFixture.SecondRefresh));
            }
            return SessionTestFixture.Json(SessionTestFixture.Profile);
        };

        var first = fixture.Session.GetProfileAsync();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = fixture.Session.GetProfileAsync();
        releaseRefresh.SetResult();
        var accounts = await Task.WhenAll(first, second);

        Assert.All(accounts, account => Assert.Equal(SessionTestFixture.Profile, account));
        var refresh = Assert.Single(fixture.Requests, request => request.Path.EndsWith("/refresh"));
        using var body = JsonDocument.Parse(refresh.Body!);
        Assert.Equal(SessionTestFixture.FirstRefresh, body.RootElement.GetProperty("refreshToken").GetString());
        Assert.Equal(SessionTestFixture.SecondRefresh, await fixture.Store.ReadAsync(default));
        Assert.All(fixture.Requests.Where(request => request.Path.EndsWith("/me")).Skip(1),
            request => Assert.Equal("access-2", request.Bearer));
        Assert.Equal(3, fixture.Requests.Count(request => request.Path.EndsWith("/me")));
    }

    // Catches preserving authentication after the refresh credential is rejected.
    [Fact]
    public async Task RejectedRefreshClearsSession()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        fixture.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<SessionExpiredException>(() => fixture.Session.GetProfileAsync());

        Assert.Null(fixture.Session.Account);
        Assert.Null(await fixture.Store.ReadAsync(default));
    }

    // Catches discarding a still recoverable session on a transient profile transport failure.
    [Fact]
    public async Task ProfileNetworkFailurePreservesRecoverableSession()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        var respond = fixture.Respond;
        fixture.Respond = (_, _) => throw new HttpRequestException("Network unavailable");

        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Session.GetProfileAsync());

        Assert.Equal(SessionTestFixture.Profile, fixture.Session.Account);
        Assert.Equal(SessionTestFixture.FirstRefresh, await fixture.Store.ReadAsync(default));
        fixture.Respond = respond;
        Assert.Equal(SessionTestFixture.Profile, await fixture.Session.GetProfileAsync());
    }

    // Catches leaving local authentication active because remote logout could not be reached.
    [Fact]
    public async Task LogoutClearsSessionWhenApiUnavailable()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (_, _) => throw new HttpRequestException("Network unavailable");

        Assert.False(await fixture.Session.SignOutAsync());

        Assert.Null(fixture.Session.Account);
        Assert.Null(await fixture.Store.ReadAsync(default));
        await Assert.ThrowsAsync<SessionExpiredException>(() => fixture.Session.GetProfileAsync());
    }

    // Catches a late rotation writing/publishing credentials after the user has signed out.
    [Fact]
    public async Task PendingRefreshCannotRestoreLoggedOutSession()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        var refreshStarted = SessionTestFixture.NewSignal();
        var releaseRefresh = SessionTestFixture.NewSignal();
        fixture.Respond = async (request, _) =>
        {
            if (request.Path.EndsWith("/refresh"))
            {
                refreshStarted.TrySetResult();
                await releaseRefresh.Task;
                return SessionTestFixture.Json(fixture.Tokens("access-2", SessionTestFixture.SecondRefresh));
            }
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        };
        var profile = fixture.Session.GetProfileAsync();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(await fixture.Session.SignOutAsync());
        releaseRefresh.SetResult();
        await Assert.ThrowsAsync<SessionExpiredException>(() => profile);

        Assert.Null(fixture.Session.Account);
        Assert.Null(await fixture.Store.ReadAsync(default));
        Assert.Single(fixture.Requests, request => request.Path.EndsWith("/me"));
    }

    // Catches publishing an account before its rotated refresh credential is saved successfully.
    [Fact]
    public async Task StorageFailureDoesNotPublishAuthenticatedAccount()
    {
        using var fixture = new SessionTestFixture(new FailingRefreshTokenStore());
        var authenticatedPublication = false;
        fixture.Session.Changed += () => authenticatedPublication |= fixture.Session.Account is not null;

        await Assert.ThrowsAsync<IOException>(() => fixture.Session.SignInAsync(SessionTestFixture.Credentials));

        Assert.Null(fixture.Session.Account);
        Assert.Null(await fixture.Store.ReadAsync(default));
        Assert.False(authenticatedPublication);
    }

    // Catches requiring a password on startup or trusting the account embedded in refresh responses.
    [Fact]
    public async Task RestoreUsesSavedRefreshWithoutPassword()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Store.WriteAsync(SessionTestFixture.FirstRefresh, default);

        var restored = await fixture.Session.RestoreAsync();

        Assert.Equal(SessionTestFixture.Profile, restored);
        Assert.Equal(SessionTestFixture.Profile, fixture.Session.Account);
        Assert.DoesNotContain(fixture.Requests, request => request.Path.EndsWith("/login"));
        var refresh = Assert.Single(fixture.Requests, request => request.Path.EndsWith("/refresh"));
        using var body = JsonDocument.Parse(refresh.Body!);
        Assert.Equal(SessionTestFixture.FirstRefresh, body.RootElement.GetProperty("refreshToken").GetString());
        Assert.False(body.RootElement.TryGetProperty("password", out _));
        Assert.Equal(SessionTestFixture.SecondRefresh, await fixture.Store.ReadAsync(default));
        Assert.Equal("access-2", Assert.Single(fixture.Requests, request => request.Path.EndsWith("/me")).Bearer);
    }

    // Catches retrying an unauthorized profile indefinitely or leaving rejected authentication active.
    [Fact]
    public async Task UnauthorizedProfileRetriesOnlyOnce()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (request, _) => Task.FromResult(request.Path.EndsWith("/refresh")
            ? SessionTestFixture.Json(fixture.Tokens("access-2", SessionTestFixture.SecondRefresh))
            : new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<SessionExpiredException>(() => fixture.Session.GetProfileAsync());

        Assert.Equal(3, fixture.Requests.Count(request => request.Path.EndsWith("/me")));
        Assert.Single(fixture.Requests, request => request.Path.EndsWith("/refresh"));
        Assert.Equal(new[] { "access-1", "access-2" }, fixture.Requests
            .Where(request => request.Path.EndsWith("/me")).Skip(1).Select(request => request.Bearer));
        Assert.Null(fixture.Session.Account);
        Assert.Null(await fixture.Store.ReadAsync(default));
    }
}
