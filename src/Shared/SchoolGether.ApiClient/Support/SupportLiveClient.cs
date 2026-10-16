using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Support;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace SchoolGether.ApiClient.Support;

public enum SupportConnectionState { Disconnected, Connecting, Connected, Reconnecting }

public sealed class SupportLiveClient : IAsyncDisposable
{
    private readonly IdentitySession session;
    private readonly Func<Uri, Func<Task<string?>>, ISupportLiveConnection> factory;
    private readonly Uri? address;
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private ActiveConnection? active;
    private Task cleanup = Task.CompletedTask;
    private bool disposed;
    public event Action<SupportChangedEvent>? Changed;
    public event Action<SupportConnectionState>? ConnectionStateChanged;
    public SupportConnectionState State { get; private set; }
    public SupportLiveClient(HttpClient http, IdentitySession session)
        : this(session, (uri, token) => new SignalRSupportConnection(uri, token), http.BaseAddress) { }
    internal SupportLiveClient(IdentitySession session, Func<Uri, Func<Task<string?>>, ISupportLiveConnection> factory, Uri? address)
    {
        this.session = session; this.factory = factory; this.address = address;
        session.Changed += SessionChanged;
    }
    public Task StartAsync(int ticketId, CancellationToken ct = default)
    {
        if (ticketId <= 0) throw new ArgumentOutOfRangeException(nameof(ticketId));
        return StartCoreAsync(ticketId, ct);
    }
    // A autorização/inscrição global pertence ao servidor; não existe um ID sentinela.
    public Task StartQueueAsync(CancellationToken ct = default) => StartCoreAsync(null, ct);
    private async Task StartCoreAsync(int? ticketId, CancellationToken ct)
    {
        await lifecycle.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            await StopCoreAsync(CancellationToken.None);
            if (address is null) throw new InvalidOperationException("MissingApiConfiguration");
            var generation = session.Generation;
            var lease = new SupportSessionLease(session, ct);
            ActiveConnection? current = null;
            var connection = factory(new Uri(address, "api/v1/support/live"), async () =>
            {
                if (session.Generation != generation) throw new SessionExpiredException();
                return await session.ExecuteAuthorizedAsync<string?>((token, _) => Task.FromResult<string?>(token), lease.Token);
            });
            current = new(connection, lease, generation);
            Volatile.Write(ref active, current);
            connection.Changed += notice =>
            {
                if (IsCurrent(current) && (ticketId is null || notice.TicketId == ticketId)) Changed?.Invoke(notice);
            };
            connection.Reconnecting += () => { if (IsCurrent(current)) SetState(SupportConnectionState.Reconnecting); return Task.CompletedTask; };
            connection.Reconnected += async () =>
            {
                if (!IsCurrent(current)) return;
                try
                {
                    if (ticketId is not null) await connection.JoinAsync(ticketId.Value, lease.Token);
                    if (IsCurrent(current)) SetState(SupportConnectionState.Connected);
                }
                catch
                {
                    if (Interlocked.CompareExchange(ref active, null, current) == current)
                    { SetState(SupportConnectionState.Disconnected); cleanup = CloseQuietlyAsync(current); }
                }
            };
            connection.Closed += () => { if (IsCurrent(current)) SetState(SupportConnectionState.Disconnected); return Task.CompletedTask; };
            SetState(SupportConnectionState.Connecting);
            try
            {
                await connection.StartAsync(lease.Token);
                if (ticketId is not null) await connection.JoinAsync(ticketId.Value, lease.Token);
                if (!IsCurrent(current)) throw new SessionExpiredException();
                SetState(SupportConnectionState.Connected);
            }
            catch
            {
                if (Interlocked.CompareExchange(ref active, null, current) == current) SetState(SupportConnectionState.Disconnected);
                await CloseQuietlyAsync(current);
                throw;
            }
        }
        finally { lifecycle.Release(); }
    }
    private bool IsCurrent(ActiveConnection current) => ReferenceEquals(Volatile.Read(ref active), current) && current.Generation == session.Generation;
    private void SetState(SupportConnectionState state) { State = state; ConnectionStateChanged?.Invoke(state); }
    private void SessionChanged()
    {
        var current = Volatile.Read(ref active);
        if (current is null || current.Generation == session.Generation) return;
        if (Interlocked.CompareExchange(ref active, null, current) != current) return;
        SetState(SupportConnectionState.Disconnected);
        cleanup = CloseQuietlyAsync(current);
    }
    public async Task StopAsync(CancellationToken ct = default)
    {
        await lifecycle.WaitAsync(ct);
        try { await StopCoreAsync(ct); }
        finally { lifecycle.Release(); }
    }
    private async Task StopCoreAsync(CancellationToken ct)
    {
        var current = Interlocked.Exchange(ref active, null);
        SetState(SupportConnectionState.Disconnected);
        if (current is null) return;
        try { await current.Connection.StopAsync(ct); }
        finally { await current.DisposeAsync(); }
    }
    private static async Task CloseQuietlyAsync(ActiveConnection current)
    {
        try { await current.Connection.StopAsync(CancellationToken.None); }
        catch { /* A limpeza local prossegue mesmo sem rede. */ }
        finally { await current.DisposeAsync(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true; session.Changed -= SessionChanged;
        await StopAsync(); await cleanup;
    }
    private sealed class ActiveConnection(ISupportLiveConnection connection, SupportSessionLease lease, long generation) : IAsyncDisposable
    {
        private int disposed;
        public ISupportLiveConnection Connection => connection;
        public long Generation => generation;
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lease.Dispose(); await connection.DisposeAsync();
        }
    }
}

internal interface ISupportLiveConnection : IAsyncDisposable
{
    event Action<SupportChangedEvent>? Changed;
    event Func<Task>? Reconnecting;
    event Func<Task>? Reconnected;
    event Func<Task>? Closed;
    Task StartAsync(CancellationToken ct);
    Task JoinAsync(int ticketId, CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}

internal sealed class SignalRSupportConnection : ISupportLiveConnection
{
    private readonly HubConnection hub;
    public event Action<SupportChangedEvent>? Changed;
    public event Func<Task>? Reconnecting;
    public event Func<Task>? Reconnected;
    public event Func<Task>? Closed;
    internal SignalRSupportConnection(Uri address, Func<Task<string?>> token,
        Func<HttpMessageHandler, HttpMessageHandler>? handlerFactory = null)
    {
        hub = new HubConnectionBuilder().WithUrl(address, options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.AccessTokenProvider = token;
            if (handlerFactory is not null) options.HttpMessageHandlerFactory = handlerFactory;
        }).WithAutomaticReconnect().Build();
        hub.On<SupportChangedEvent>("SupportChanged", notice => Changed?.Invoke(notice));
        hub.Reconnecting += _ => Reconnecting?.Invoke() ?? Task.CompletedTask;
        hub.Reconnected += _ => Reconnected?.Invoke() ?? Task.CompletedTask;
        hub.Closed += _ => Closed?.Invoke() ?? Task.CompletedTask;
    }
    public Task StartAsync(CancellationToken ct) => hub.StartAsync(ct);
    public Task JoinAsync(int ticketId, CancellationToken ct) => hub.InvokeAsync("JoinTicket", ticketId, ct);
    public Task StopAsync(CancellationToken ct) => hub.StopAsync(ct);
    public ValueTask DisposeAsync() => hub.DisposeAsync();
}
