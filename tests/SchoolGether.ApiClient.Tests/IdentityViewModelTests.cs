using System.Net;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Mobile.Identity;

namespace SchoolGether.ApiClient.Tests;

public sealed class IdentityViewModelTests
{
    // Catches omitting login or trusting the profile embedded in the token response.
    [Fact]
    public async Task LoginPublishesProfile()
    {
        using var fixture = new SessionTestFixture();
        var model = CreateModel(fixture);
        await model.LoginCommand.ExecuteAsync(null);
        Assert.Equal("Aluno", model.Account?.Name);
        Assert.Equal("", model.Password);
        Assert.Null(model.Error);
        Assert.False(model.IsBusy);
    }

    // Catches presenting rejected credentials as success or retaining the password.
    [Fact]
    public async Task InvalidLoginShowsError()
    {
        using var fixture = new SessionTestFixture();
        fixture.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var model = CreateModel(fixture);
        await model.LoginCommand.ExecuteAsync(null);
        Assert.NotNull(model.Error);
        Assert.Contains("palavra-passe", model.Error);
        Assert.Null(model.Account);
        Assert.Equal("", model.Password);
    }

    // Catches issuing two login requests while the first submission is pending.
    [Fact]
    public async Task ConcurrentSubmitIsIgnored()
    {
        using var fixture = new SessionTestFixture();
        var release = SessionTestFixture.NewSignal();
        var respond = fixture.Respond;
        fixture.Respond = async (request, cancellation) =>
        {
            if (request.Path.EndsWith("/login")) await release.Task;
            return await respond(request, cancellation);
        };
        var model = CreateModel(fixture);
        var first = model.LoginCommand.ExecuteAsync(null);
        var second = model.LoginCommand.ExecuteAsync(null);
        try
        {
            Assert.True(model.IsBusy);
            Assert.Single(fixture.Requests, r => r.Path.EndsWith("/login"));
        }
        finally { release.SetResult(); await Task.WhenAll(first, second); }
        Assert.Equal("Aluno", model.Account?.Name);
    }

    // Catches a logout leaving the previous user's profile visible.
    [Fact]
    public async Task LogoutClearsVisibleProfile()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        var model = CreateModel(fixture);
        await model.LogoutCommand.ExecuteAsync(null);
        Assert.Null(model.Account);
        Assert.Null(await fixture.Store.ReadAsync(default));
    }

    // Catches losing the saved session or hiding the retry path after transient startup failure.
    [Fact]
    public async Task RestoreNetworkFailureOffersRetry()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Store.WriteAsync(SessionTestFixture.FirstRefresh, default);
        var respond = fixture.Respond;
        fixture.Respond = (_, _) => throw new HttpRequestException("Do not display this detail");
        var model = new IdentityViewModel(fixture.Session);
        await model.RestoreCommand.ExecuteAsync(null);
        Assert.True(model.CanRetryRestore);
        Assert.NotNull(model.Error);
        Assert.DoesNotContain("Do not display", model.Error);
        Assert.Null(model.Account);
        fixture.Respond = respond;
        await model.RestoreCommand.ExecuteAsync(null);
        Assert.Equal("Aluno", model.Account?.Name);
        Assert.False(model.CanRetryRestore);
    }

    // Catches a rejected saved refresh token silently becoming an empty startup session.
    [Fact]
    public async Task RejectedSavedSessionExplainsExpiryAndAllowsNewLogin()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Store.WriteAsync(SessionTestFixture.FirstRefresh, default);
        var respond = fixture.Respond;
        fixture.Respond = (request, cancellation) => request.Path.EndsWith("/refresh")
            ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))
            : respond(request, cancellation);
        var model = CreateModel(fixture);

        await model.RestoreCommand.ExecuteAsync(null);

        Assert.Equal("A sessão terminou. Entre novamente.", model.Error);
        Assert.Null(model.Account);
        Assert.Null(await fixture.Store.ReadAsync(default));
        Assert.False(model.CanRetryRestore);
        Assert.True(model.LoginCommand.CanExecute(null));

        model.Password = "test-password";
        await model.LoginCommand.ExecuteAsync(null);
        Assert.Null(model.Error);
        Assert.Equal("Aluno", model.Account?.Name);
    }

    // Catches treating a first launch without a saved token as an expired session.
    [Fact]
    public async Task RestoreWithoutSavedSessionStaysSilent()
    {
        using var fixture = new SessionTestFixture();
        var model = CreateModel(fixture);

        await model.RestoreCommand.ExecuteAsync(null);

        Assert.Null(model.Error);
        Assert.Null(model.Account);
        Assert.Empty(fixture.Requests);
        Assert.True(model.LoginCommand.CanExecute(null));
    }

    // Catches showing authenticated success when the protected store cannot persist the refresh token.
    [Fact]
    public async Task StorageFailureShowsSafeError()
    {
        using var fixture = new SessionTestFixture(new FailingRefreshTokenStore());
        var model = CreateModel(fixture);
        await model.LoginCommand.ExecuteAsync(null);
        Assert.NotNull(model.Error);
        Assert.Null(model.Account);
        Assert.Equal("", model.Password);
    }

    // Catches misreporting rate limits as incorrect credentials.
    [Fact]
    public async Task RateLimitOffersLaterRetry()
    {
        using var fixture = new SessionTestFixture();
        fixture.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var model = CreateModel(fixture);
        await model.LoginCommand.ExecuteAsync(null);
        Assert.Contains("tentativas", model.Error);
        Assert.Equal("", model.Password);
    }

    // Catches a cleared visible profile being reported as safely removed from protected storage.
    [Fact]
    public async Task LogoutStorageFailureOffersCleanupRetry()
    {
        var store = new SwitchableStore();
        using var fixture = new SessionTestFixture(store);
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        var model = CreateModel(fixture);
        store.Fail = true;
        await model.LogoutCommand.ExecuteAsync(null);
        Assert.Null(model.Account);
        Assert.NotNull(model.Error);
        Assert.Null(model.StatusMessage);
        Assert.True(model.CanRetryLogout);
        Assert.False(model.CanInteract);
        Assert.True(model.LogoutCommand.CanExecute(null));
        store.Fail = false;
        await model.LogoutCommand.ExecuteAsync(null);
        Assert.False(model.CanRetryLogout);
        Assert.Null(await store.ReadAsync(default));
    }

    // Catches exposing a server error or claiming remote logout when only local logout succeeded.
    [Fact]
    public async Task LogoutNetworkFailureReportsLocalOnly()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Respond = (_, _) => throw new HttpRequestException("Private endpoint detail");
        var model = CreateModel(fixture);
        await model.LogoutCommand.ExecuteAsync(null);
        Assert.Null(model.Account);
        Assert.Contains("Não foi possível confirmar", model.StatusMessage);
        Assert.Null(model.Error);
        Assert.DoesNotContain("Private endpoint", model.StatusMessage);
    }

    // Catches notifying bound properties on the HTTP worker rather than the consumer's dispatcher.
    [Fact]
    public async Task BackgroundSessionChangesUseConsumerDispatcher()
    {
        using var fixture = new SessionTestFixture();
        var queued = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        var notifications = new List<string?>();
        var model = new IdentityViewModel(fixture.Session, queued.Enqueue);
        model.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        await Task.Run(() => fixture.Session.SignInAsync(SessionTestFixture.Credentials));
        Assert.Empty(notifications);
        while (queued.TryDequeue(out var action)) action();
        Assert.Contains(nameof(model.Account), notifications);
        Assert.Contains(nameof(model.IsAuthenticated), notifications);
        Assert.Equal("Aluno", model.Account?.Name);
    }

    // Catches allowing requests to an unconfigured production endpoint.
    [Fact]
    public async Task ConfigurationErrorBlocksRequests()
    {
        using var fixture = new SessionTestFixture();
        var model = new IdentityViewModel(fixture.Session, configurationError: "Configuração em falta");
        await model.LoginCommand.ExecuteAsync(null);
        await model.RestoreCommand.ExecuteAsync(null);
        Assert.False(model.CanInteract);
        Assert.False(model.LoginCommand.CanExecute(null));
        Assert.False(model.RestoreCommand.CanExecute(null));
        Assert.NotNull(model.Error);
        Assert.Empty(fixture.Requests);
    }

    // Catches sending invalid form data or retaining a password after local validation failure.
    [Fact]
    public async Task InvalidFormDoesNotContactApi()
    {
        using var fixture = new SessionTestFixture();
        var model = CreateModel(fixture);
        model.Email = "invalid email";
        await model.LoginCommand.ExecuteAsync(null);
        Assert.NotNull(model.Error);
        Assert.Equal("", model.Password);
        Assert.Empty(fixture.Requests);
    }

    // Catches stale profile bindings after refresh rejection and password visibility leaking across submissions.
    [Fact]
    public async Task RejectedProfileRefreshReturnsToLogin()
    {
        using var fixture = new SessionTestFixture();
        await fixture.Session.SignInAsync(SessionTestFixture.Credentials);
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        fixture.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var model = CreateModel(fixture);
        model.TogglePasswordCommand.Execute(null);
        Assert.False(model.IsPasswordHidden);
        await model.RefreshProfileCommand.ExecuteAsync(null);
        Assert.True(model.IsLoginVisible);
        Assert.Null(model.Account);
        Assert.Contains("sessão terminou", model.Error);
        Assert.True(model.IsPasswordHidden);
    }

    private sealed class SwitchableStore : IRefreshTokenStore
    {
        private readonly MemoryRefreshTokenStore inner = new();
        public bool Fail { get; set; }
        public Task<string?> ReadAsync(CancellationToken token) => inner.ReadAsync(token);
        public Task WriteAsync(string? value, CancellationToken token)
            => Fail ? throw new IOException("Private storage detail") : inner.WriteAsync(value, token);
    }

    private static IdentityViewModel CreateModel(SessionTestFixture fixture) => new(fixture.Session)
    {
        Email = "student@example.test", Password = "test-password"
    };
}
