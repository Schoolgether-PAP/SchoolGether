using SchoolGether.ApiClient.Support;
using SchoolGether.Contracts.Support;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Channels;

namespace SchoolGether.ApiClient.Tests;

public sealed class SupportLiveClientTests
{
    [Fact]
    public async Task OfficialSignalRTransportUsesLongPollingBearerHeadersAndAuthorizedDelete()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        using var server = new LongPollingServer();
        await using var live = new SupportLiveClient(f.Session, (uri, token) => new SignalRSupportConnection(uri, token, _ => server), f.Http.BaseAddress!);
        var notice = new TaskCompletionSource<SupportChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        live.Changed += value => notice.TrySetResult(value);
        await live.StartQueueAsync().WaitAsync(TimeSpan.FromSeconds(10));
        server.Deliver("{\"type\":1,\"target\":\"SupportChanged\",\"arguments\":[{\"ticketId\":3,\"rowVersion\":41}]}\u001e");
        Assert.Equal(new(3, 41), await notice.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        await live.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains(server.Requests, r => r.Method == "GET");
        Assert.Contains(server.Requests, r => r.Method == "DELETE");
        Assert.All(server.Requests, r => { Assert.Equal("Bearer access-1", r.Bearer); Assert.DoesNotContain("access_token", r.Path); Assert.DoesNotContain("access-1", r.Path); });
    }
    [Fact]
    public async Task ConnectAndReconnectRejoinTicketWithCurrentSessionToken()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var connection = new LiveConnection(); Func<Task<string?>>? tokenProvider = null;
        await using var live = new SupportLiveClient(f.Session, (uri, token) =>
        {
            Assert.Equal("/api/v1/support/live", uri.AbsolutePath); Assert.Equal("", uri.Query);
            tokenProvider = token; return connection;
        }, f.Http.BaseAddress!);
        var states = new List<SupportConnectionState>(); live.ConnectionStateChanged += states.Add;
        await live.StartAsync(3);
        Assert.Equal("access-1", await tokenProvider!());
        f.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal("access-2", await tokenProvider());
        await connection.ReconnectAsync();
        Assert.Equal(new[] { 3, 3 }, connection.Joins);
        Assert.Contains(SupportConnectionState.Reconnecting, states);
        Assert.Equal(SupportConnectionState.Connected, live.State);
    }

    [Fact]
    public async Task QueueHasNoInventedTicketAndLateNoticesAfterLogoutAreIgnored()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var connection = new LiveConnection();
        await using var live = new SupportLiveClient(f.Session, (_, _) => connection, f.Http.BaseAddress!);
        var notices = new List<SupportChangedEvent>(); live.Changed += notices.Add;
        await live.StartQueueAsync();
        connection.Notify(new(3, 9)); Assert.Single(notices); Assert.Empty(connection.Joins);
        await f.Session.SignOutAsync(); connection.Notify(new(3, 10));
        Assert.Single(notices); Assert.Equal(SupportConnectionState.Disconnected, live.State);
        Assert.True(connection.Stopped);
    }

    [Fact]
    public async Task InitialFailureCanBeRetriedAndDoesNotRemoveHttpSession()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var attempts = 0;
        await using var live = new SupportLiveClient(f.Session, (_, _) => new LiveConnection { FailStart = attempts++ == 0 }, f.Http.BaseAddress!);
        await Assert.ThrowsAsync<HttpRequestException>(() => live.StartAsync(3));
        Assert.Equal(SupportConnectionState.Disconnected, live.State); Assert.NotNull(f.Session.Account);
        await live.StartAsync(3); Assert.Equal(SupportConnectionState.Connected, live.State);
    }

    internal sealed class LiveConnection : ISupportLiveConnection
    {
        public event Action<SupportChangedEvent>? Changed;
        public event Func<Task>? Reconnecting;
        public event Func<Task>? Reconnected;
        public event Func<Task>? Closed;
        public List<int> Joins { get; } = [];
        public bool FailStart { get; set; }
        public bool Stopped { get; private set; }
        public Task StartAsync(CancellationToken ct) => FailStart ? Task.FromException(new HttpRequestException("offline")) : Task.CompletedTask;
        public Task JoinAsync(int id, CancellationToken ct) { Joins.Add(id); return Task.CompletedTask; }
        public Task StopAsync(CancellationToken ct) { Stopped = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Stopped = true; return ValueTask.CompletedTask; }
        public void Notify(SupportChangedEvent notice) => Changed?.Invoke(notice);
        public async Task ReconnectAsync() { if (Reconnecting is not null) await Reconnecting(); if (Reconnected is not null) await Reconnected(); }
        public Task DisconnectAsync() => Closed?.Invoke() ?? Task.CompletedTask;
    }

    private sealed class LongPollingServer : HttpMessageHandler
    {
        private readonly Channel<string> messages = Channel.CreateUnbounded<string>();
        private int polls;
        public ConcurrentQueue<(string Method, string Path, string? Bearer)> Requests { get; } = new();
        public void Deliver(string message) => messages.Writer.TryWrite(message);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Enqueue((request.Method.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.ToString()));
            if (request.RequestUri.AbsolutePath.EndsWith("/negotiate"))
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { connectionId = "id", connectionToken = "connection", negotiateVersion = 1,
                    availableTransports = new[] { new { transport = "WebSockets", transferFormats = new[] { "Text", "Binary" } }, new { transport = "LongPolling", transferFormats = new[] { "Text", "Binary" } } } }) };
            if (request.Method == HttpMethod.Delete) return new(HttpStatusCode.Accepted);
            if (request.Method == HttpMethod.Post)
            {
                var body = await request.Content!.ReadAsStringAsync(ct);
                Assert.DoesNotContain("access-1", body);
                if (body.Contains("protocol")) Deliver("{}\u001e");
                return new(HttpStatusCode.OK);
            }
            var message = Interlocked.Increment(ref polls) == 1 ? "" : await messages.Reader.ReadAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(message, Encoding.UTF8, "application/octet-stream") };
        }
    }
}
