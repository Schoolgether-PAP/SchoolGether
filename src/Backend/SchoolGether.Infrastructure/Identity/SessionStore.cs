using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Identity;

internal sealed class SessionStore(SchoolGetherDbContext db) : ISessionStore
{
    public async Task AddAsync(Session session, CancellationToken cancellationToken)
    {
        db.Sessions.Add(ToEntity(session));
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Session?> FindAsync(string hash, CancellationToken cancellationToken) => db.Sessions.AsNoTracking()
        .Where(x => x.TokenHash == hash).Select(x => new Session(x.TokenHash, x.UserId, x.FamilyId,
            x.SecurityStamp, DateTime.SpecifyKind(x.ExpiresAt, DateTimeKind.Utc),
            x.UsedAt != null || x.RevokedAt != null || x.DeletedAt != null))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> RotateAsync(Session previous, Session next, DateTime now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Atualização condicional impede que dois pedidos consumam o mesmo token.
        var updated = await db.Sessions.Where(x => x.TokenHash == previous.Hash && x.UsedAt == null &&
                x.RevokedAt == null && x.DeletedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, now)
                .SetProperty(x => x.UpdatedAt, now).SetProperty(x => x.RowVersion, x => x.RowVersion + 1), cancellationToken);
        if (updated == 0)
        {
            await RevokeFamilyAsync(previous.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        db.Sessions.Add(ToEntity(next));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken)
    {
        await db.Sessions.Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.UpdatedAt, now).SetProperty(x => x.RowVersion, x => x.RowVersion + 1), cancellationToken);
    }

    private static RefreshSession ToEntity(Session session) => new() { TokenHash = session.Hash,
        UserId = session.UserId, FamilyId = session.FamilyId, SecurityStamp = session.SecurityStamp,
        ExpiresAt = session.ExpiresAt, CreatedBy = session.UserId };
}
