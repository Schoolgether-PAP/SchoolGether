using System.Net;
using System.Text.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.ApiClient.Support;
using SchoolGether.Contracts.Support;

namespace SchoolGether.ApiClient.Tests;

public sealed class SupportConversationStateTests
{
    [Fact]
    public async Task SendAcknowledgementDoesNotSkipMessagesMissedBeforeItsId()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        await using var live = OfflineLive(f); await using var state = new SupportConversationState(new(f.Http, f.Session), live, f.Session);
        f.Respond = (r, _) => Task.FromResult(r.Method == "POST" ? SessionTestFixture.Json(SupportApiClientTests.Message(3)) : r.Path switch
        {
            "/api/v1/support/tickets/3/messages?take=50" => Page([1], false),
            "/api/v1/support/tickets/3/messages?take=50&afterId=1" => Page([2, 3], false),
            "/api/v1/support/tickets/3/messages?take=50&afterId=3" => Page([], false),
            _ => SessionTestFixture.Json(SupportApiClientTests.Ticket())
        });
        await state.LoadAsync(3); await state.SetDraftAsync("Resposta"); await state.SendDraftAsync();
        Assert.Equal(new[] { 1, 2, 3 }, state.Messages.Select(m => m.Id));
    }

    [Fact]
    public async Task SendingDraftCannotBeReplacedOrQueuedForAutomaticDuplicateSend()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        await using var live = OfflineLive(f); await using var state = new SupportConversationState(new(f.Http, f.Session), live, f.Session);
        var started = SessionTestFixture.NewSignal(); var release = SessionTestFixture.NewSignal();
        f.Respond = async (r, _) =>
        {
            if (r.Method == "POST") { started.SetResult(); await release.Task; throw new HttpRequestException("lost"); }
            return r.Path.Contains("messages") ? Page([], false) : SessionTestFixture.Json(SupportApiClientTests.Ticket());
        };
        await state.LoadAsync(3); await state.SetDraftAsync("Primeiro");
        var send = state.SendDraftAsync(); await started.Task;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => state.SetDraftAsync("Outro"));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<InvalidOperationException>(() => state.SendDraftAsync(cancellation.Token));
        }
        finally { release.SetResult(); }
        await Assert.ThrowsAsync<HttpRequestException>(() => send);
        Assert.Equal("Primeiro", state.Draft!.Text); Assert.True(state.DeliveryUncertain);
    }

    [Fact]
    public async Task RefreshRecoversEveryAfterPageDeduplicatesAndOlderUsesBefore()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        await using var live = OfflineLive(f);
        await using var state = new SupportConversationState(new(f.Http, f.Session), live, f.Session);
        var refreshing = false;
        f.Respond = (r, _) => Task.FromResult(r.Path switch
        {
            "/api/v1/support/tickets/3" => SessionTestFixture.Json(SupportApiClientTests.Ticket(version: refreshing ? 42 : 9)),
            "/api/v1/support/tickets/3/messages?take=50" => Page([2, 3], true),
            "/api/v1/support/tickets/3/messages?take=50&afterId=3" => Page([3, 4], true),
            "/api/v1/support/tickets/3/messages?take=50&afterId=4" => Page([4, 5], false),
            "/api/v1/support/tickets/3/messages?take=50&beforeId=2" => Page([1], false),
            _ => throw new InvalidOperationException(r.Path)
        });
        await state.LoadAsync(3); refreshing = true;
        await state.RefreshAsync(); await state.LoadOlderAsync();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, state.Messages.Select(m => m.Id));
        Assert.Equal(42, state.Ticket!.RowVersion); Assert.False(state.HasOlderMessages);
    }

    [Fact]
    public async Task ConflictPreservesDraftAndUpdatesServerVersionWithoutResending()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        await using var live = OfflineLive(f); await using var state = new SupportConversationState(new(f.Http, f.Session), live, f.Session);
        var version = 9L; var sends = 0;
        f.Respond = (r, _) => Task.FromResult(r.Method == "POST" ? Conflict() : r.Path.Contains("messages") ? Page([], false) : SessionTestFixture.Json(SupportApiClientTests.Ticket(version: version)));
        HttpResponseMessage Conflict() { sends++; version = 71; return new(HttpStatusCode.Conflict); }
        await state.LoadAsync(3);
        await state.SetDraftAsync("Manter texto", files: [new("a.txt", 3, _ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])))]);
        var draft = state.Draft;
        await Assert.ThrowsAsync<IdentityApiException>(() => state.SendDraftAsync());
        Assert.Same(draft, state.Draft); Assert.Single(state.Draft!.Files);
        Assert.Equal(71, state.Ticket!.RowVersion); Assert.Equal(1, sends);
    }

    [Fact]
    public async Task UncertainSendKeepsKeyAndFingerprintUntilExplicitContentReplacement()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        await using var live = OfflineLive(f); await using var state = new SupportConversationState(new(f.Http, f.Session), live, f.Session);
        var sends = new List<string>();
        f.Respond = (r, _) =>
        {
            if (r.Method == "POST") { sends.Add(r.Body!); throw new HttpRequestException("lost response"); }
            return Task.FromResult(r.Path.Contains("messages") ? Page([], false) : SessionTestFixture.Json(SupportApiClientTests.Ticket()));
        };
        await state.LoadAsync(3); await state.SetDraftAsync("Primeiro"); var draft = state.Draft!;
        await Assert.ThrowsAsync<HttpRequestException>(() => state.SendDraftAsync());
        Assert.True(state.DeliveryUncertain);
        await state.RefreshAsync(); Assert.Single(sends);
        await Assert.ThrowsAsync<InvalidOperationException>(() => state.SetDraftAsync("Outro"));
        await Assert.ThrowsAsync<HttpRequestException>(() => state.SendDraftAsync());
        Assert.Same(draft, state.Draft); Assert.All(sends, body => Assert.Contains(draft.ClientId.ToString(), body));
        await state.SetDraftAsync("Outro", replaceUncertain: true);
        Assert.NotEqual(draft.ClientId, state.Draft!.ClientId); Assert.NotEqual(draft.Fingerprint, state.Draft.Fingerprint);
    }

    [Fact]
    public async Task OldTicketResponseCannotReplaceNewTicketAndLogoutClearsFiles()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        await using var live = OfflineLive(f); await using var state = new SupportConversationState(new(f.Http, f.Session), live, f.Session);
        var started = SessionTestFixture.NewSignal(); var release = SessionTestFixture.NewSignal();
        f.Respond = async (r, _) =>
        {
            if (r.Path.EndsWith("/logout")) return new(HttpStatusCode.NoContent);
            if (r.Path.EndsWith("/3")) { started.TrySetResult(); await release.Task; return SessionTestFixture.Json(SupportApiClientTests.Ticket(3)); }
            return r.Path.Contains("messages") ? Page([], false) : SessionTestFixture.Json(SupportApiClientTests.Ticket(4));
        };
        var old = state.LoadAsync(3); await started.Task;
        var next = state.LoadAsync(4); release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => old); await next;
        Assert.Equal(4, state.Ticket!.Id);
        await state.SetDraftAsync("texto", files: [new("a.txt", 1, _ => Task.FromResult<Stream>(new MemoryStream([1])))]);
        await f.Session.SignOutAsync();
        Assert.Null(state.Ticket); Assert.Empty(state.Messages); Assert.Null(state.Draft);
    }

    internal static SupportLiveClient OfflineLive(SessionTestFixture f) => new(f.Session, (_, _) => new SupportLiveClientTests.LiveConnection { FailStart = true }, f.Http.BaseAddress!);
    [Fact]
    public async Task ReconnectRecoversMessagesAndTerminalTicketEvenWithoutMessageNotice()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var connection = new SupportLiveClientTests.LiveConnection();
        await using var live = new SupportLiveClient(f.Session, (_, _) => connection, f.Http.BaseAddress!);
        await using var state = new SupportConversationState(new(f.Http, f.Session), live, f.Session);
        var closed = false;
        f.Respond = (r, _) => Task.FromResult(r.Path.Contains("messages")
            ? Page(closed ? [2] : [1], false)
            : SessionTestFixture.Json(SupportApiClientTests.Ticket(version: closed ? 81 : 9) with { Status = closed ? "Closed" : "Open", CanReply = !closed }));
        await state.LoadAsync(3); closed = true;
        var recovered = SessionTestFixture.NewSignal(); state.Changed += () => { if (state.Ticket?.RowVersion == 81) recovered.TrySetResult(); };
        await connection.ReconnectAsync(); await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { 1, 2 }, state.Messages.Select(m => m.Id)); Assert.False(state.Ticket!.CanReply);
        Assert.Equal(new[] { 3, 3 }, connection.Joins);
    }

    [Fact]
    public async Task RecoveryBoundsResidentMessagesAndAllowsOlderPagesAfterTrimming()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        await using var live = OfflineLive(f); await using var state = new SupportConversationState(new(f.Http, f.Session), live, f.Session);
        f.Respond = (r, _) =>
        {
            if (!r.Path.Contains("messages")) return Task.FromResult(SessionTestFixture.Json(SupportApiClientTests.Ticket()));
            if (r.Path.Contains("beforeId=101")) return Task.FromResult(Page(Enumerable.Range(51, 50).ToArray(), true));
            var after = r.Path.Contains("afterId=") ? int.Parse(r.Path.Split("afterId=")[1]) : 0;
            var ids = Enumerable.Range(after + 1, Math.Min(50, 600 - after)).ToArray();
            return Task.FromResult(Page(ids, after is > 0 and < 550));
        };
        await state.LoadAsync(3); await state.RefreshAsync();
        Assert.Equal(500, state.Messages.Count); Assert.Equal(101, state.Messages[0].Id); Assert.True(state.HasOlderMessages);
        await state.LoadOlderAsync(); Assert.Equal(500, state.Messages.Count); Assert.Equal(51, state.Messages[0].Id);
        await state.RefreshAsync();
        Assert.Equal(Enumerable.Range(101, 500), state.Messages.Select(m => m.Id));
        Assert.False(state.RecoveryPending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UncertainRetryKeepsAttemptedVersionAndRecognizesPersistedReplay(bool firstAttemptPersisted)
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        await using var live = OfflineLive(f); await using var state = new SupportConversationState(new(f.Http, f.Session), live, f.Session);
        var version = 9L; var requests = new List<SendSupportMessageRequest>(); var writes = 0;
        SupportMessageResponse? saved = null;
        f.Respond = (r, _) =>
        {
            if (r.Method != "POST") return Task.FromResult(r.Path.Contains("messages")
                ? SessionTestFixture.Json(new SupportMessagePageResponse(saved is null ? [] : [saved], false))
                : SessionTestFixture.Json(SupportApiClientTests.Ticket(version: version)));
            var start = r.Body!.IndexOf('{'); var end = r.Body.IndexOf('}', start);
            var request = JsonSerializer.Deserialize<SendSupportMessageRequest>(r.Body[start..(end + 1)], new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            requests.Add(request);
            if (saved?.ClientId == request.ClientId) return Task.FromResult(SessionTestFixture.Json(saved));
            if (request.RowVersion != version) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict));
            if (requests.Count > 1 || firstAttemptPersisted)
            {
                saved = SupportApiClientTests.Message(1) with { ClientId = request.ClientId };
                writes++; version++;
            }
            if (requests.Count == 1) throw new HttpRequestException("Confirmação perdida");
            return Task.FromResult(SessionTestFixture.Json(saved!));
        };
        await state.LoadAsync(3);
        await state.SetDraftAsync("Conservar", files: [new("a.txt", 1, _ => Task.FromResult<Stream>(new MemoryStream([1])))]);
        var draft = state.Draft;
        await Assert.ThrowsAsync<HttpRequestException>(() => state.SendDraftAsync());
        version = 71; await state.RefreshAsync();
        Assert.True(state.DeliveryUncertain); Assert.Same(draft, state.Draft); Assert.Equal(71, state.Ticket!.RowVersion);
        if (firstAttemptPersisted)
        {
            await state.SendDraftAsync();
            Assert.Null(state.Draft); Assert.Single(state.Messages); Assert.Equal(1, writes);
        }
        else
        {
            var conflict = await Assert.ThrowsAsync<IdentityApiException>(() => state.SendDraftAsync());
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            Assert.Same(draft, state.Draft); Assert.Single(state.Draft!.Files); Assert.Equal(0, writes);
            Assert.False(state.DeliveryUncertain);
        }
        Assert.Equal(new long[] { 9, 9 }, requests.Select(r => r.RowVersion));
        Assert.All(requests, request => Assert.Equal(draft!.ClientId, request.ClientId));
        if (!firstAttemptPersisted)
        {
            // Uma nova ação explícita após rever o 409 pode usar a versão apresentada.
            await state.SendDraftAsync(); Assert.Equal(71, requests[^1].RowVersion);
            Assert.Equal(draft!.ClientId, requests[^1].ClientId); Assert.Equal(1, writes); Assert.Null(state.Draft);
        }
    }

    [Fact]
    public async Task ChangedAttachmentBytesCannotReuseAnUncertainDraftFingerprint()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        await using var live = OfflineLive(f); await using var state = new SupportConversationState(new(f.Http, f.Session), live, f.Session);
        var sent = false; byte content = 1;
        f.Respond = (r, _) => { if (r.Method == "POST") sent = true; return Task.FromResult(r.Path.Contains("messages") ? Page([], false) : SessionTestFixture.Json(SupportApiClientTests.Ticket())); };
        await state.LoadAsync(3); await state.SetDraftAsync("texto", files: [new("a.txt", 1, _ => Task.FromResult<Stream>(new MemoryStream([content])))]);
        content = 2;
        await Assert.ThrowsAsync<HttpRequestException>(() => state.SendDraftAsync());
        Assert.False(sent); Assert.NotNull(state.Draft);
    }
    private static HttpResponseMessage Page(int[] ids, bool more) => SessionTestFixture.Json(new SupportMessagePageResponse(ids.Select(SupportApiClientTests.Message).ToArray(), more));
}
