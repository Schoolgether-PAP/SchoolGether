using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Support;
using SchoolGether.Infrastructure.Persistence;
using SchoolGether.Infrastructure.Support;
namespace SchoolGether.Api.IntegrationTests;
public sealed partial class SupportStorageTests
{
    [Fact]
    public async Task CleanupAfterRestart_DeletesOnlyOldUnreferencedFiles_AndRetainsActiveFiles()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        using var body = SupportAttachmentTests.Multipart(SupportAuthorizationTests.Create(), ("saved.txt", "saved"u8.ToArray()));
        (await client.PostAsync(SupportAuthorizationTests.Route, body)).EnsureSuccessStatusCode();
        var original = factory.Services.GetRequiredService<IFileStorage>();
        async Task<PreparedAttachment> Prepare(bool commit)
        {
            using var bytes = new MemoryStream("text"u8.ToArray()); var file = await original.PrepareAsync(new("note.txt", bytes), default);
            if (commit) await original.CommitAsync(file, default); return file;
        }
        var orphan = await Prepare(true); var temporary = await Prepare(false); var recent = await Prepare(true);
        var options = factory.Services.GetRequiredService<SupportOptions>();
        // Novo processo: não herda as preparações que ficaram entre disco e SQL.
        var restarted = new LocalFileStorage(options);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var referenced = (await db.SupportAttachments.IgnoreQueryFilters().SingleAsync()).StorageKey;
        foreach (var (folder, key) in new[] { ("files", orphan.StorageKey), ("temporary", temporary.StorageKey), ("files", referenced) })
            File.SetLastWriteTimeUtc(Path.Combine(options.FilesRoot, folder, key), DateTime.UtcNow.AddHours(-25));
        using var activeData = new MemoryStream("active"u8.ToArray()); var active = await restarted.PrepareAsync(new("active.txt", activeData), default);
        File.SetLastWriteTimeUtc(Path.Combine(options.FilesRoot, "temporary", active.StorageKey), DateTime.UtcNow.AddHours(-25));
        await new OrphanFileCleanup(db, restarted, TimeProvider.System).RunOnceAsync(default);
        Assert.False(File.Exists(Path.Combine(options.FilesRoot, "files", orphan.StorageKey)));
        Assert.False(File.Exists(Path.Combine(options.FilesRoot, "temporary", temporary.StorageKey)));
        Assert.True(File.Exists(Path.Combine(options.FilesRoot, "files", referenced)));
        Assert.True(File.Exists(Path.Combine(options.FilesRoot, "files", recent.StorageKey)));
        Assert.True(File.Exists(Path.Combine(options.FilesRoot, "temporary", active.StorageKey)));
        await restarted.CommitAsync(active, default);
        File.SetLastWriteTimeUtc(Path.Combine(options.FilesRoot, "files", active.StorageKey), DateTime.UtcNow.AddHours(-25));
        await new OrphanFileCleanup(db, restarted, TimeProvider.System).RunOnceAsync(default);
        Assert.True(File.Exists(Path.Combine(options.FilesRoot, "files", active.StorageKey)));
        await restarted.ReleaseAsync(active.StorageKey, default);
        await new OrphanFileCleanup(db, restarted, TimeProvider.System).RunOnceAsync(default);
        Assert.False(File.Exists(Path.Combine(options.FilesRoot, "files", active.StorageKey)));
    }
}
