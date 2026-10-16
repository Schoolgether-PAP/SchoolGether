namespace SchoolGether.Application.Support;

public interface IFileStorage
{
    Task<PreparedAttachment> PrepareAsync(IncomingAttachment file, CancellationToken ct);
    Task CommitAsync(PreparedAttachment file, CancellationToken ct);
    Task ReleaseAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
}


// Possui apenas as preparações desta operação; a conclusão SQL decide se os ficheiros ficam.
internal sealed class SupportFileBatch(IFileStorage storage) : IAsyncDisposable
{
    internal List<PreparedAttachment> Files { get; } = [];
    private bool persisted;
    internal static async Task<SupportFileBatch> PrepareAsync(IFileStorage storage, IReadOnlyList<IncomingAttachment> incoming, CancellationToken ct)
    {
        if (incoming is null || incoming.Count > 3) throw new SupportException("InvalidSupportAttachment");
        var batch = new SupportFileBatch(storage);
        try
        {
            foreach (var file in incoming)
            {
                batch.Files.Add(await storage.PrepareAsync(file, ct));
                if (batch.Files.Sum(x => x.Size) > 15 * 1024 * 1024) throw new SupportException("InvalidSupportAttachment");
            }
            return batch;
        }
        catch { await batch.DisposeAsync(); throw; }
    }
    internal async Task CommitFilesAsync(CancellationToken ct)
    { foreach (var file in Files) await storage.CommitAsync(file, ct); }
    internal void MarkPersisted() => persisted = true;
    public async ValueTask DisposeAsync()
    {
        foreach (var file in Files)
        {
            try { if (!persisted) await storage.DeleteAsync(file.StorageKey, CancellationToken.None); }
            catch (SupportException) { /* Um erro de disco deixa órfão recuperável após 24 horas. */ }
            finally { await storage.ReleaseAsync(file.StorageKey, CancellationToken.None); }
        }
    }
}
