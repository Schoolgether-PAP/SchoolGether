using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SchoolGether.Application.Platform;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Platform;

internal sealed class DashboardPreferenceStore(SchoolGetherDbContext db, TimeProvider clock) : IDashboardPreferenceStore
{
    public async Task<DashboardPreference?> FindAsync(int accountId, CancellationToken cancellationToken)
    {
        var entry = await db.Set<DashboardPreferenceEntry>().AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == accountId, cancellationToken);
        return entry is null ? null : Map(entry);
    }
    public async Task<DashboardPreference> SaveAsync(int accountId, DashboardPreference preference, CancellationToken cancellationToken)
    {
        var entry = await db.Set<DashboardPreferenceEntry>().SingleOrDefaultAsync(x => x.AccountId == accountId, cancellationToken);
        if ((entry?.RowVersion ?? 0) != preference.RowVersion) throw new PlatformException("VersionConflict");
        if (entry is null)
        {
            entry = new DashboardPreferenceEntry { AccountId = accountId, CreatedAt = clock.GetUtcNow().UtcDateTime };
            db.Add(entry);
        }
        else entry.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        entry.SchemaVersion = preference.SchemaVersion;
        entry.WidgetsJson = JsonSerializer.Serialize(preference.Widgets);
        entry.RowVersion++;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 } ||
            (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" &&
             exception.InnerException?.Message.Contains("UNIQUE constraint failed:", StringComparison.Ordinal) == true))
        {
            // Duas primeiras gravações expected=0 competem pela chave da própria conta.
            throw new PlatformException("VersionConflict");
        }
        return Map(entry);
    }
    private static DashboardPreference Map(DashboardPreferenceEntry entry) =>
        new(entry.SchemaVersion, entry.RowVersion, JsonSerializer.Deserialize<DashboardWidget[]>(entry.WidgetsJson)!);
}
