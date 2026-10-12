using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Diagnostics;

namespace SchoolGether.Infrastructure.Persistence;

internal sealed class DatabaseReadiness(SchoolGetherDbContext dbContext) : IDatabaseReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return false;
        }

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        return !pendingMigrations.Any();
    }
}
