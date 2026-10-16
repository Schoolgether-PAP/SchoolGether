namespace SchoolGether.Application.Support;

public interface IFileStorage
{
    Task<PreparedAttachment> PrepareAsync(IncomingAttachment file, CancellationToken ct);
    Task CommitAsync(PreparedAttachment file, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
}
