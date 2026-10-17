using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Support;

namespace SchoolGether.ApiClient.Support;

public sealed record SupportUpload(string FileName, long Size, Func<CancellationToken, Task<Stream>> OpenReadAsync)
{
    internal string? Sha256 { get; init; }
}

public sealed class SupportDownload : IAsyncDisposable, IDisposable
{
    private readonly HttpResponseMessage response;
    private readonly SupportSessionLease lease;
    private readonly CancellationTokenRegistration registration;
    public Stream Content { get; }
    public string? FileName => response.Content.Headers.ContentDisposition?.FileNameStar
        ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
    public string? ContentType => response.Content.Headers.ContentType?.MediaType;
    internal SupportDownload(HttpResponseMessage response, Stream content, SupportSessionLease lease)
    {
        this.response = response; Content = content; this.lease = lease;
        registration = lease.Token.Register(response.Dispose);
    }
    public void Dispose() { registration.Dispose(); response.Dispose(); lease.Dispose(); }
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}

public sealed class SupportApiClient(HttpClient http, IdentitySession session)
{
    public const long MaximumFileSize = 5 * 1024 * 1024;
    private const string Root = "api/v1/support/";
    public Task<SupportContextResponse> ContextAsync(CancellationToken ct = default) => JsonAsync<SupportContextResponse>(HttpMethod.Get, Root + "context", null, ct);
    public Task<IReadOnlyList<SupportAssigneeResponse>> AssigneesAsync(CancellationToken ct = default) => JsonAsync<IReadOnlyList<SupportAssigneeResponse>>(HttpMethod.Get, Root + "assignees", null, ct);
    public Task<SupportTicketPageResponse> ListAsync(int page = 1, int pageSize = 20, string? search = null,
        string? status = null, string? priority = null, int? institutionId = null, int? assigneeId = null, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("InvalidPagination");
        var query = $"tickets?page={page}&pageSize={pageSize}";
        foreach (var (name, value) in new (string, string?)[] { ("search", search), ("status", status), ("priority", priority),
            ("institutionId", institutionId?.ToString(CultureInfo.InvariantCulture)), ("assigneeId", assigneeId?.ToString(CultureInfo.InvariantCulture)) })
            if (value is not null) query += $"&{name}={Uri.EscapeDataString(value)}";
        return JsonAsync<SupportTicketPageResponse>(HttpMethod.Get, Root + query, null, ct);
    }
    public Task<SupportTicketResponse> CreateAsync(CreateSupportTicketRequest request, IReadOnlyList<SupportUpload>? files = null, CancellationToken ct = default)
        => MultipartAsync<SupportTicketResponse>("tickets", request, files, ct);
    public Task<SupportTicketResponse> GetAsync(int id, CancellationToken ct = default)
        => JsonAsync<SupportTicketResponse>(HttpMethod.Get, Root + $"tickets/{id}", null, ct);
    public Task<SupportTicketResponse> UpdateAsync(int id, UpdateSupportTicketRequest request, CancellationToken ct = default)
        => JsonAsync<SupportTicketResponse>(HttpMethod.Put, Root + $"tickets/{id}", request, ct);
    public Task<SupportTicketResponse> CloseAsync(int id, CloseSupportTicketRequest request, CancellationToken ct = default)
        => JsonAsync<SupportTicketResponse>(HttpMethod.Post, Root + $"tickets/{id}/close", request, ct);
    public Task<SupportMessageResponse> SendAsync(int id, SendSupportMessageRequest request, IReadOnlyList<SupportUpload>? files = null, CancellationToken ct = default)
        => MultipartAsync<SupportMessageResponse>($"tickets/{id}/messages", request, files, ct);
    public Task<SupportAdminMessageResponse> SendAdminAsync(int id, SendSupportMessageRequest request, IReadOnlyList<SupportUpload>? files = null, CancellationToken ct = default)
        => MultipartAsync<SupportAdminMessageResponse>($"tickets/{id}/messages", request, files, ct);
    public Task<SupportMessagePageResponse> MessagesAsync(int id, int? beforeId = null, int? afterId = null, int take = 50, CancellationToken ct = default)
        => MessagePageAsync<SupportMessagePageResponse>(id, beforeId, afterId, take, ct);
    public Task<SupportAdminMessagePageResponse> AdminMessagesAsync(int id, int? beforeId = null, int? afterId = null, int take = 50, CancellationToken ct = default)
        => MessagePageAsync<SupportAdminMessagePageResponse>(id, beforeId, afterId, take, ct);
    private async Task<T> MessagePageAsync<T>(int id, int? before, int? after, int take, CancellationToken ct)
    {
        if ((before.HasValue && after.HasValue) || before is <= 0 || after is <= 0 || take is < 1 or > 50)
            throw new ArgumentException("InvalidCursor");
        var query = $"tickets/{id}/messages?take={take}";
        if (before is not null) query += $"&beforeId={before}";
        if (after is not null) query += $"&afterId={after}";
        return await JsonAsync<T>(HttpMethod.Get, Root + query, null, ct);
    }
    private async Task<T> JsonAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var lease = new SupportSessionLease(session, ct);
        return await session.ExecuteAuthorizedAsync(async (token, cancellation) =>
        {
            using var request = Authorized(method, path, token);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, cancellation);
            return await ApiResponseReader.ReadAsync<T>(response, cancellation);
        }, lease.Token);
    }
    private async Task<T> MultipartAsync<T>(string path, object body, IReadOnlyList<SupportUpload>? files, CancellationToken ct)
    {
        var snapshot = files?.ToArray() ?? [];
        ValidateUploads(snapshot);
        using var lease = new SupportSessionLease(session, ct);
        return await session.ExecuteAuthorizedAsync(async (token, cancellation) =>
        {
            using var request = Authorized(HttpMethod.Post, Root + path, token);
            using var content = new MultipartFormDataContent();
            request.Content = content;
            content.Add(JsonContent.Create(body), "request");
            foreach (var file in snapshot)
            {
                var stream = await file.OpenReadAsync(cancellation);
                try { content.Add(new StreamContent(new ExactLengthStream(stream, file.Size, file.Sha256)), "files", file.FileName); }
                catch { await stream.DisposeAsync(); throw; }
            }
            using var response = await http.SendAsync(request, cancellation);
            return await ApiResponseReader.ReadAsync<T>(response, cancellation);
        }, lease.Token);
    }
    internal static void ValidateUploads(IReadOnlyList<SupportUpload> files)
    {
        if (files.Count > 3 || files.Any(f => f.Size is <= 0 or > MaximumFileSize || string.IsNullOrWhiteSpace(f.FileName)
            || f.FileName.Length > 255 || f.FileName.Any(char.IsControl)) || files.Sum(f => f.Size) > 15 * 1024 * 1024)
            throw new ArgumentException("InvalidSupportAttachment");
    }
    public async Task<SupportDownload> DownloadAsync(int id, CancellationToken ct = default)
    {
        var lease = new SupportSessionLease(session, ct);
        SupportDownload? download = null;
        try
        {
            return await session.ExecuteAuthorizedAsync(async (token, cancellation) =>
            {
                using var request = Authorized(HttpMethod.Get, Root + $"attachments/{id}", token);
                var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
                try
                {
                    await ApiResponseReader.EnsureSuccessAsync(response, cancellation);
                    download = new(response, await response.Content.ReadAsStreamAsync(cancellation), lease);
                    return download;
                }
                catch { response.Dispose(); throw; }
            }, lease.Token);
        }
        catch { download?.Dispose(); lease.Dispose(); throw; }
    }
    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

// Não depende de Length/Seek (IBrowserFile); verifica também a dimensão física declarada.
internal sealed class ExactLengthStream(Stream inner, long expected, string? expectedHash = null) : Stream
{
    private long read;
    private readonly IncrementalHash? hash = expectedHash is null ? null : IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool completed;
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => read; set => throw new NotSupportedException(); }
    private int Check(ReadOnlySpan<byte> bytes)
    {
        var count = bytes.Length;
        read += count;
        if (read > expected || (count == 0 && read != expected)) throw new IOException("InvalidSupportAttachmentSize");
        if (!completed)
        {
            hash?.AppendData(bytes);
            if (count == 0)
            {
                completed = true;
                if (hash is not null && Convert.ToHexString(hash.GetHashAndReset()) != expectedHash) throw new IOException("SupportAttachmentChanged");
            }
        }
        return count;
    }
    public override int Read(byte[] buffer, int offset, int count) => Check(buffer.AsSpan(offset, inner.Read(buffer, offset, count)));
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var count = await inner.ReadAsync(buffer, ct); return Check(buffer.Span[..count]);
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
    protected override void Dispose(bool disposing) { if (disposing) { hash?.Dispose(); inner.Dispose(); } base.Dispose(disposing); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
