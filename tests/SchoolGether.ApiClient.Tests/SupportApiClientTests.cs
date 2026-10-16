using System.Net;
using SchoolGether.ApiClient.Identity;
using SchoolGether.ApiClient.Support;
using SchoolGether.Contracts.Support;

namespace SchoolGether.ApiClient.Tests;

public sealed class SupportApiClientTests
{
    internal static SupportTicketResponse Ticket(int id = 3, long version = 9) => new(id, 2, 7, "Docente", "Ajuda", "Other", "Normal", "Open", null, null, version, DateTime.UtcNow, null, null, null, true, false, false);
    internal static SupportMessageResponse Message(int id) => new(id, Guid.NewGuid(), 7, "Docente", $"Mensagem {id}", DateTime.UtcNow, []);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public async Task MultipartRefreshReopensAllStreamsWithSamePayloadAndDisposesThem(int count)
    {
        using var f = new SessionTestFixture();
        await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var streams = new List<TrackingStream>();
        var files = Enumerable.Range(0, count).Select(i => new SupportUpload($"file{i}.txt", 3, _ =>
        {
            var stream = new TrackingStream([65, 66, 67]); streams.Add(stream);
            return Task.FromResult<Stream>(stream);
        })).ToArray();
        var bodies = new List<string>();
        f.Respond = (r, _) => Task.FromResult(r.Path.EndsWith("/refresh") ? SessionTestFixture.Json(f.Tokens("access-2")) : Reply(r));
        HttpResponseMessage Reply(SessionRequest r)
        {
            Assert.Equal("Bearer", r.Scheme);
            Assert.DoesNotContain("access", r.Path);
            bodies.Add(r.Body!);
            return bodies.Count == 1 ? new(HttpStatusCode.Unauthorized) : SessionTestFixture.Json(Ticket());
        }
        var request = new CreateSupportTicketRequest(Guid.NewGuid(), "Ajuda", "Other", "Normal", "Descrição");
        var result = await new SupportApiClient(f.Http, f.Session).CreateAsync(request, files);
        Assert.Equal(9, result.RowVersion);
        Assert.Equal(count * 2, streams.Count);
        Assert.All(streams, stream => Assert.True(stream.Disposed));
        Assert.All(bodies, body => { Assert.Contains(request.ClientId.ToString(), body); Assert.Equal(count, body.Split("filename=").Length - 1); });
        Assert.Equal(new[] { "access-1", "access-2" }, f.Requests.Where(r => r.Path.EndsWith("/support/tickets")).Select(r => r.Bearer));
    }

    [Theory]
    [InlineData(400)] [InlineData(403)] [InlineData(404)] [InlineData(409)] [InlineData(413)] [InlineData(429)]
    public async Task HttpErrorsPreserveStatusWithoutRetry(int code)
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        f.Respond = (_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code));
        var error = await Assert.ThrowsAsync<IdentityApiException>(() => new SupportApiClient(f.Http, f.Session).GetAsync(3));
        Assert.Equal((HttpStatusCode)code, error.StatusCode);
        Assert.Single(f.Requests, r => r.Path.Contains("support"));
        Assert.NotNull(f.Session.Account);
    }

    [Fact]
    public async Task RepeatedUnauthorizedExpiresSession()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        f.Respond = (r, _) => Task.FromResult(r.Path.EndsWith("/refresh") ? SessionTestFixture.Json(f.Tokens("access-2")) : new(HttpStatusCode.Unauthorized));
        await Assert.ThrowsAsync<SessionExpiredException>(() => new SupportApiClient(f.Http, f.Session).GetAsync(3));
        Assert.Null(f.Session.Account);
    }

    [Fact]
    public async Task DownloadOwnsResponseStream()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var stream = new TrackingStream([1, 2, 3]);
        f.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) });
        var download = await new SupportApiClient(f.Http, f.Session).DownloadAsync(8);
        Assert.False(stream.Disposed);
        Assert.Equal(1, download.Content.ReadByte());
        await download.DisposeAsync();
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task LogoutCancelsPendingHttp()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var started = SessionTestFixture.NewSignal(); var cancelled = SessionTestFixture.NewSignal();
        f.Respond = async (r, ct) =>
        {
            if (r.Path.EndsWith("/logout")) return new(HttpStatusCode.NoContent);
            started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); } catch (OperationCanceledException) { cancelled.TrySetResult(); throw; }
            return SessionTestFixture.Json(Ticket());
        };
        var api = new SupportApiClient(f.Http, f.Session);
        var pending = api.GetAsync(3); await started.Task;
        await f.Session.SignOutAsync(); await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<SessionExpiredException>(() => pending);
    }

    [Fact]
    public async Task CallerCancellationCancelsPendingHttpWithoutLoggingOut()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var started = SessionTestFixture.NewSignal();
        f.Respond = async (_, ct) => { started.SetResult(); await Task.Delay(Timeout.Infinite, ct); return SessionTestFixture.Json(Ticket()); };
        using var cancel = new CancellationTokenSource();
        var operation = new SupportApiClient(f.Http, f.Session).GetAsync(3, cancel.Token);
        await started.Task; cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation); Assert.NotNull(f.Session.Account);
    }

    [Fact]
    public async Task LogoutDisposesOpenDownloadAndLateResponse()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var stream = new TrackingStream([1]);
        f.Respond = (r, _) => Task.FromResult(r.Path.EndsWith("/logout") ? new(HttpStatusCode.NoContent) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) });
        await using var download = await new SupportApiClient(f.Http, f.Session).DownloadAsync(8);
        await f.Session.SignOutAsync(); Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task PartialStreamFactoryFailureDisposesAlreadyOpenedFiles()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var stream = new TrackingStream([1]);
        await Assert.ThrowsAsync<IOException>(() => new SupportApiClient(f.Http, f.Session).CreateAsync(new(Guid.NewGuid(), "a", "Other", "Normal", "b"),
            [new("one.txt", 1, _ => Task.FromResult<Stream>(stream)), new("two.txt", 1, _ => throw new IOException("unavailable"))]));
        Assert.True(stream.Disposed); Assert.DoesNotContain(f.Requests, r => r.Path.Contains("support"));
    }

    [Fact]
    public async Task TypedOperationsUseExpectedRoutesJsonAndEscapedFilters()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        f.Respond = (r, _) => Task.FromResult(r.Path switch
        {
            "/api/v1/support/context" => SessionTestFixture.Json(new SupportContextResponse(true, true, false, false)),
            "/api/v1/support/assignees" => SessionTestFixture.Json(new[] { new SupportAssigneeResponse(4, "Agente") }),
            "/api/v1/support/tickets?page=2&pageSize=10&search=a%26b&status=Open&priority=High&institutionId=2&assigneeId=4" => SessionTestFixture.Json(new SupportTicketPageResponse([Ticket()], 11, 2, 10)),
            "/api/v1/support/tickets/3/messages?take=7&beforeId=9" => SessionTestFixture.Json(new SupportAdminMessagePageResponse([new(1, Guid.NewGuid(), 7, "Agente", "Nota", DateTime.UtcNow, [], true)], false)),
            "/api/v1/support/tickets/3/messages" => SessionTestFixture.Json(Message(1)),
            _ => SessionTestFixture.Json(Ticket(version: 74))
        });
        var api = new SupportApiClient(f.Http, f.Session);
        Assert.True((await api.ContextAsync()).Enabled); Assert.Single(await api.AssigneesAsync());
        Assert.Equal(11, (await api.ListAsync(2, 10, "a&b", "Open", "High", 2, 4)).Total);
        Assert.True(Assert.Single((await api.AdminMessagesAsync(3, beforeId: 9, take: 7)).Items).Internal);
        Assert.Equal(74, (await api.UpdateAsync(3, new(9, "InProgress", "High", 4))).RowVersion);
        Assert.Equal(74, (await api.CloseAsync(3, new(74, "Resolvido"))).RowVersion);
        await api.SendAsync(3, new(Guid.NewGuid(), 74, "texto", false));
        Assert.Contains(f.Requests, r => r.Method == "PUT" && r.Body!.Contains("\"rowVersion\":9"));
        Assert.Contains(f.Requests, r => r.Path.EndsWith("/close") && r.Body!.Contains("Resolvido"));
        Assert.All(f.Requests.Where(r => r.Path.Contains("support")), r => Assert.Equal("access-1", r.Bearer));
    }

    [Fact]
    public async Task RejectsExcessiveFilesAndInvalidCursorsBeforeSending()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var api = new SupportApiClient(f.Http, f.Session);
        var upload = new SupportUpload("a.txt", 5 * 1024 * 1024 + 1, _ => throw new InvalidOperationException());
        await Assert.ThrowsAsync<ArgumentException>(() => api.CreateAsync(new(Guid.NewGuid(), "a", "Other", "Normal", "b"), [upload]));
        await Assert.ThrowsAsync<ArgumentException>(() => api.MessagesAsync(3, beforeId: 2, afterId: 3));
        Assert.DoesNotContain(f.Requests, r => r.Path.Contains("support"));
    }

    internal sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
