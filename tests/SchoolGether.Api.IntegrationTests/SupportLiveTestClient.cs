using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SchoolGether.Api.IntegrationTests;

internal sealed class SupportLiveTestClient : IAsyncDisposable
{
    internal const string Path = "/api/v1/support/live";
    private readonly Channel<JsonElement> events = Channel.CreateUnbounded<JsonElement>();
    internal HubConnection Connection { get; }
    internal ConcurrentQueue<(HttpMethod Method, bool HasAuthorization, bool HasQueryToken, int Status)> Requests { get; } = new();

    internal SupportLiveTestClient(WebApplicationFactory<Program> factory, HttpClient authenticated,
        HttpTransportType transport = HttpTransportType.LongPolling, string? origin = null)
    {
        var token = authenticated.DefaultRequestHeaders.Authorization!.Parameter!;
        Connection = new HubConnectionBuilder().WithUrl("https://localhost" + Path, options =>
        {
            options.Transports = transport;
            options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            options.HttpMessageHandlerFactory = _ => new ObservingHandler(factory.Server.CreateHandler(), Requests);
            if (origin is not null) options.Headers["Origin"] = origin;
            options.WebSocketFactory = async (context, ct) =>
            {
                Assert.DoesNotContain("access_token", context.Uri.Query);
                var sockets = factory.Server.CreateWebSocketClient();
                sockets.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + token;
                return await sockets.ConnectAsync(context.Uri, ct);
            };
        }).Build();
        Connection.On<JsonElement>("SupportChanged", value => events.Writer.TryWrite(value));
    }

    internal Task StartAsync() => Connection.StartAsync();
    internal Task JoinAsync(int ticketId) => Connection.InvokeAsync("JoinTicket", ticketId);
    internal async Task<JsonElement> NextAsync() => await events.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    internal async Task AssertQuietAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await events.Reader.ReadAsync(timeout.Token));
    }
    public ValueTask DisposeAsync() => Connection.DisposeAsync();

    private sealed class ObservingHandler(HttpMessageHandler inner,
        ConcurrentQueue<(HttpMethod Method, bool HasAuthorization, bool HasQueryToken, int Status)> requests) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            requests.Enqueue((request.Method, request.Headers.Authorization?.Scheme == "Bearer",
                request.RequestUri!.Query.Contains("access_token", StringComparison.OrdinalIgnoreCase), (int)response.StatusCode));
            return response;
        }
    }
}
