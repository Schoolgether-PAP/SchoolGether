using Microsoft.EntityFrameworkCore;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Support;

public sealed class OrphanFileCleanup(SchoolGetherDbContext db, LocalFileStorage storage, TimeProvider clock)
{
    public async Task RunOnceAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddHours(-24);
        foreach (var key in storage.OrphanCandidates(cutoff))
        {
            ct.ThrowIfCancellationRequested();
            // Mesmo referências eliminadas logicamente conservam o histórico físico.
            if (await db.SupportAttachments.IgnoreQueryFilters().AnyAsync(x => x.StorageKey == key, ct)) continue;
            if (!storage.IsActive(key)) await storage.DeleteAsync(key, ct);
        }
    }
}
