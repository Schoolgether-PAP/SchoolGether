using System.Collections.Concurrent;
using System.Security.Cryptography;
using SchoolGether.Application.Support;

namespace SchoolGether.Infrastructure.Support;

public sealed class LocalFileStorage : IFileStorage
{
    private static readonly SemaphoreSlim PreparationSlots = new(2, 2);
    private readonly ConcurrentDictionary<string, byte> active = new();
    internal string Root { get; }
    internal string TemporaryRoot => Path.Combine(Root, "temporary");
    internal string FinalRoot => Path.Combine(Root, "files");
    public LocalFileStorage(SupportOptions options)
    {
        if (!Path.IsPathFullyQualified(options.FilesRoot) || options.InstitutionStorageLimitBytes <= 0)
            throw new InvalidOperationException("Support:FilesRoot exige um caminho privado absoluto e a quota deve ser positiva.");
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.FilesRoot));
        if (Root == Path.GetPathRoot(Root)) throw new InvalidOperationException("Support:FilesRoot não pode ser a raiz do volume.");
        EnsurePrivateDirectories();
    }
    private void EnsurePrivateDirectories()
    {
        for (var path = new DirectoryInfo(Root); path is not null; path = path.Parent)
            if (path.Exists && path.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("SupportStorageLinkDenied");
        Directory.CreateDirectory(Root);
        foreach (var folder in new[] { TemporaryRoot, FinalRoot })
        {
            Directory.CreateDirectory(folder);
            if (File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("SupportStorageLinkDenied");
        }
    }
    private string FilePath(string folder, string key)
    {
        if (key.Length != 32 || key.Any(x => !char.IsAsciiHexDigit(x))) throw new SupportException("SupportFileUnavailable");
        EnsurePrivateDirectories();
        var path = Path.GetFullPath(Path.Combine(folder, key));
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))) throw new SupportException("SupportFileUnavailable");
        return path;
    }
    public async Task<PreparedAttachment> PrepareAsync(IncomingAttachment file, CancellationToken ct)
    {
        AttachmentValidator.ValidateName(file.FileName);
        await PreparationSlots.WaitAsync(ct);
        var key = Guid.NewGuid().ToString("N"); active.TryAdd(key, 0);
        try
        {
            var path = FilePath(TemporaryRoot, key);
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920]; long size = 0; int read;
            while ((read = await file.Content.ReadAsync(buffer, ct)) > 0)
            {
                size += read;
                if (size > AttachmentValidator.MaximumBytes) throw new SupportException("InvalidSupportAttachment");
                hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            if (size == 0) throw new SupportException("InvalidSupportAttachment");
            await output.FlushAsync(ct); output.Position = 0;
            var mime = await AttachmentValidator.ValidateAsync(output, file.FileName, ct);
            return new(key, file.FileName, mime, size, Convert.ToHexString(hash.GetHashAndReset()));
        }
        catch (Exception ex)
        {
            try { await DeleteAsync(key, CancellationToken.None); } catch (SupportException) { }
            if (ex is IOException or UnauthorizedAccessException) throw new SupportException("SupportFileUnavailable");
            throw;
        }
        finally { PreparationSlots.Release(); }
    }
    public Task CommitAsync(PreparedAttachment file, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            File.Move(FilePath(TemporaryRoot, file.StorageKey), FilePath(FinalRoot, file.StorageKey));
            File.SetLastWriteTimeUtc(FilePath(FinalRoot, file.StorageKey), DateTime.UtcNow);
            return Task.CompletedTask;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new SupportException("SupportFileUnavailable"); }
    }
    public Task ReleaseAsync(string key, CancellationToken ct)
    { active.TryRemove(key, out _); return Task.CompletedTask; }
    public Task DeleteAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            File.Delete(FilePath(TemporaryRoot, key)); File.Delete(FilePath(FinalRoot, key));
            active.TryRemove(key, out _); return Task.CompletedTask;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { active.TryRemove(key, out _); throw new SupportException("SupportFileUnavailable"); }
    }
    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try { return Task.FromResult<Stream>(new FileStream(FilePath(FinalRoot, key), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new SupportException("SupportFileUnavailable"); }
    }
    internal IEnumerable<string> OrphanCandidates(DateTime cutoff)
    {
        EnsurePrivateDirectories();
        return new[] { TemporaryRoot, FinalRoot }.SelectMany(Directory.EnumerateFiles)
            .Where(path => Path.GetFileName(path).Length == 32 && Path.GetFileName(path).All(char.IsAsciiHexDigit) &&
                !active.ContainsKey(Path.GetFileName(path)) && !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) && File.GetLastWriteTimeUtc(path) < cutoff)
            .Select(Path.GetFileName).OfType<string>().Distinct().ToArray();
    }
    internal bool IsActive(string key) => active.ContainsKey(key);
}
