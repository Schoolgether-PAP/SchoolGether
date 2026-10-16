using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Support;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SchoolGether.ApiClient.Support;

public sealed record SupportConversationMessage(int Id, Guid ClientId, int AuthorId, string AuthorName, string Text,
    DateTime CreatedAt, IReadOnlyList<SupportAttachmentResponse> Attachments, bool Internal);
public sealed record SupportMessageDraft(Guid ClientId, string Text, bool Internal, IReadOnlyList<SupportUpload> Files, string Fingerprint);

public sealed class SupportConversationState : IAsyncDisposable
{
    private readonly SupportApiClient api;
    private readonly SupportLiveClient live;
    private readonly IdentitySession session;
    private readonly object gate = new();
    private readonly SemaphoreSlim reads = new(1, 1);
    private readonly SemaphoreSlim sends = new(1, 1);
    private Conversation? current;
    private long sessionGeneration;
    private bool disposed;
    private Task background = Task.CompletedTask;
    public event Action? Changed;
    public SupportTicketResponse? Ticket { get; private set; }
    public IReadOnlyList<SupportConversationMessage> Messages { get; private set; } = [];
    public SupportMessageDraft? Draft { get; private set; }
    public bool HasOlderMessages { get; private set; }
    public bool RecoveryPending { get; private set; }
    public bool DeliveryUncertain { get; private set; }
    public bool IsSending => sends.CurrentCount == 0;
    public Exception? Error { get; private set; }
    public Exception? LiveError { get; private set; }
    public SupportConversationState(SupportApiClient api, SupportLiveClient live, IdentitySession session)
    {
        this.api = api; this.live = live; this.session = session;
        sessionGeneration = session.Generation;
        session.Changed += SessionChanged;
        live.Changed += LiveChanged;
        live.ConnectionStateChanged += ConnectionChanged;
    }
    public async Task LoadAsync(int ticketId, CancellationToken ct = default)
    {
        if (ticketId <= 0) throw new ArgumentOutOfRangeException(nameof(ticketId));
        Conversation context;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ClearCore(); current = context = new(ticketId, session.Generation);
        }
        Changed?.Invoke();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, context.Token);
        await live.StopAsync(linked.Token);
        await reads.WaitAsync(linked.Token);
        try
        {
            var ticket = await api.GetAsync(ticketId, linked.Token);
            var page = await PageAsync(context, null, null, linked.Token);
            Apply(context, () =>
            {
                Ticket = ticket; HasOlderMessages = page.HasMore;
                Merge(context, page.Items, older: false);
            });
        }
        finally { reads.Release(); }
        try
        {
            await live.StartAsync(ticketId, linked.Token);
            Apply(context, () => context.LiveStarted = true);
            // Fecha também a janela entre a leitura inicial e a inscrição.
            await RefreshAsync(linked.Token);
        }
        catch (OperationCanceledException) { throw; }
        catch (SessionExpiredException) { throw; }
        catch (Exception ex)
        {
            Apply(context, () => LiveError = ex);
        }
    }
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var context = Context();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, context.Token);
        await reads.WaitAsync(linked.Token);
        try
        {
            for (var pageNumber = 0; pageNumber < 20; pageNumber++)
            {
                int after;
                lock (gate) { RequireCurrent(context); after = context.AfterId; }
                var page = await PageAsync(context, null, after > 0 ? after : null, linked.Token);
                var progressed = false;
                Apply(context, () =>
                {
                    Merge(context, page.Items, older: false);
                    progressed = context.AfterId > after; RecoveryPending = page.HasMore;
                });
                if (!page.HasMore || !progressed) break;
            }
            var ticket = await api.GetAsync(context.TicketId, linked.Token);
            Apply(context, () => { if (Ticket is null || ticket.RowVersion >= Ticket.RowVersion) Ticket = ticket; Error = null; });
        }
        finally { reads.Release(); }
    }
    public async Task LoadOlderAsync(CancellationToken ct = default)
    {
        var context = Context();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, context.Token);
        await reads.WaitAsync(linked.Token);
        try
        {
            int? before;
            lock (gate) { RequireCurrent(context); if (!HasOlderMessages) return; before = Messages.Count == 0 ? null : Messages.Min(m => m.Id); }
            var page = await PageAsync(context, before, null, linked.Token);
            Apply(context, () => { HasOlderMessages = page.HasMore; Merge(context, page.Items, older: true); });
        }
        finally { reads.Release(); }
    }
    public async Task SetDraftAsync(string text, bool internalNote = false, IReadOnlyList<SupportUpload>? files = null,
        bool replaceUncertain = false, CancellationToken ct = default)
    {
        var context = Context();
        if (IsSending) throw new InvalidOperationException("SupportSendInProgress");
        var snapshot = files?.ToArray() ?? [];
        SupportApiClient.ValidateUploads(snapshot);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, context.Token);
        var verified = new List<SupportUpload>();
        foreach (var file in snapshot)
        {
            await using var stream = new ExactLengthStream(await file.OpenReadAsync(linked.Token), file.Size);
            var hash = System.Convert.ToHexString(await SHA256.HashDataAsync(stream, linked.Token));
            verified.Add(file with { Sha256 = hash });
        }
        var fingerprint = System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { text, internalNote, files = verified.Select(f => new { f.FileName, f.Size, f.Sha256 }) }))));
        Apply(context, () =>
        {
            if (IsSending) throw new InvalidOperationException("SupportSendInProgress");
            if (Draft?.Fingerprint == fingerprint) return;
            if (DeliveryUncertain && !replaceUncertain) throw new InvalidOperationException("ConfirmDraftReplacement");
            Draft = new(Guid.NewGuid(), text, internalNote, verified.AsReadOnly(), fingerprint);
            DeliveryUncertain = false;
        });
    }
    public async Task SendDraftAsync(CancellationToken ct = default)
    {
        var context = Context();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, context.Token);
        if (!await sends.WaitAsync(0, linked.Token)) throw new InvalidOperationException("SupportSendInProgress");
        try
        {
            SupportMessageDraft draft; long version;
            lock (gate) { RequireCurrent(context); draft = Draft ?? throw new InvalidOperationException("MissingDraft"); version = Ticket?.RowVersion ?? throw new InvalidOperationException("MissingTicket"); }
            try
            {
                var request = new SendSupportMessageRequest(draft.ClientId, version, draft.Text, draft.Internal);
                SupportConversationMessage message;
                if (session.Account?.Role == "PlatformAdmin") message = Convert(await api.SendAdminAsync(context.TicketId, request, draft.Files, linked.Token));
                else message = Convert(await api.SendAsync(context.TicketId, request, draft.Files, linked.Token));
                Apply(context, () =>
                {
                    // A confirmação pode saltar IDs ainda não lidos; só páginas HTTP avançam o cursor.
                    Merge(context, [message], older: false, advanceCursor: false);
                    if (ReferenceEquals(Draft, draft)) { Draft = null; DeliveryUncertain = false; }
                });
            }
            catch (IdentityApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                try { await RefreshAsync(linked.Token); } catch (Exception refreshError) { Apply(context, () => Error = refreshError); }
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                // Um transporte interrompido pode ter perdido apenas a confirmação do envio.
                if (ex is not IdentityApiException)
                    lock (gate) { if (IsCurrent(context)) DeliveryUncertain = true; }
                throw;
            }
            await RefreshAsync(linked.Token);
        }
        finally { sends.Release(); Changed?.Invoke(); }
    }
    private async Task<(IReadOnlyList<SupportConversationMessage> Items, bool HasMore)> PageAsync(Conversation context, int? before, int? after, CancellationToken ct)
    {
        if (session.Account?.Role == "PlatformAdmin")
        {
            var page = await api.AdminMessagesAsync(context.TicketId, before, after, ct: ct);
            return (page.Items.Select(Convert).ToArray(), page.HasMore);
        }
        var schoolPage = await api.MessagesAsync(context.TicketId, before, after, ct: ct);
        return (schoolPage.Items.Select(Convert).ToArray(), schoolPage.HasMore);
    }
    private static SupportConversationMessage Convert(SupportMessageResponse m) => new(m.Id, m.ClientId, m.AuthorId, m.AuthorName, m.Text, m.CreatedAt, m.Attachments, false);
    private static SupportConversationMessage Convert(SupportAdminMessageResponse m) => new(m.Id, m.ClientId, m.AuthorId, m.AuthorName, m.Text, m.CreatedAt, m.Attachments, m.Internal);
    private void Merge(Conversation context, IReadOnlyList<SupportConversationMessage> incoming, bool older, bool advanceCursor = true)
    {
        if (advanceCursor && incoming.Count > 0) context.AfterId = Math.Max(context.AfterId, incoming.Max(m => m.Id));
        var merged = Messages.Concat(incoming).DistinctBy(m => m.Id).DistinctBy(m => m.ClientId).OrderBy(m => m.Id).ToArray();
        if (merged.Length > 500)
        {
            if (!older) HasOlderMessages = true;
            merged = older ? merged.Take(500).ToArray() : merged.TakeLast(500).ToArray();
        }
        Messages = Array.AsReadOnly(merged);
    }
    private Conversation Context() { lock (gate) return current ?? throw new InvalidOperationException("MissingConversation"); }
    private bool IsCurrent(Conversation context) => ReferenceEquals(current, context) && session.Generation == context.Generation && !context.Token.IsCancellationRequested;
    private void RequireCurrent(Conversation context) { if (!IsCurrent(context)) throw new OperationCanceledException(context.Token); }
    private void Apply(Conversation context, Action action) { lock (gate) { RequireCurrent(context); action(); } Changed?.Invoke(); }
    private void LiveChanged(SupportChangedEvent notice)
    {
        lock (gate) { if (current is { } context && IsCurrent(context) && context.TicketId == notice.TicketId) background = RecoverQuietlyAsync(context); }
    }
    private void ConnectionChanged(SupportConnectionState state)
    {
        lock (gate) { if (state == SupportConnectionState.Connected && current is { LiveStarted: true } context && IsCurrent(context)) background = RecoverQuietlyAsync(context); }
    }
    private async Task RecoverQuietlyAsync(Conversation context)
    {
        try { await RefreshAsync(context.Token); }
        catch (Exception ex)
        {
            lock (gate) { if (!IsCurrent(context)) return; Error = ex; }
            Changed?.Invoke();
        }
    }
    private void SessionChanged()
    {
        if (sessionGeneration == session.Generation) return;
        sessionGeneration = session.Generation; Clear();
    }
    private void ClearCore()
    {
        current?.Dispose(); current = null;
        Ticket = null; Messages = []; Draft = null; Error = null; LiveError = null;
        HasOlderMessages = false; DeliveryUncertain = false; RecoveryPending = false;
    }
    public void Clear()
    {
        lock (gate) ClearCore();
        background = StopQuietlyAsync(); Changed?.Invoke();
    }
    private async Task StopQuietlyAsync() { try { await live.StopAsync(); } catch { /* Estado local já limpo. */ } }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true; session.Changed -= SessionChanged; live.Changed -= LiveChanged; live.ConnectionStateChanged -= ConnectionChanged;
        Clear(); await background;
    }
    private sealed class Conversation : IDisposable
    {
        private readonly CancellationTokenSource cancellation = new();
        public int TicketId { get; }
        public long Generation { get; }
        public CancellationToken Token { get; }
        public Conversation(int ticketId, long generation) { TicketId = ticketId; Generation = generation; Token = cancellation.Token; }
        public int AfterId { get; set; }
        public bool LiveStarted { get; set; }
        public void Dispose() { cancellation.Cancel(); cancellation.Dispose(); }
    }
}
