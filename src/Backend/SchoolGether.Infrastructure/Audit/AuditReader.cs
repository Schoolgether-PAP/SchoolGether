using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Audit;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Audit;

internal sealed class AuditReader(SchoolGetherDbContext db) : IAuditReader
{
    public async Task<AuditPage> ListAsync(int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        var query = db.AuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Action.ToLower().Contains(term) || x.Target.ToLower().Contains(term) ||
                x.ActorName.ToLower().Contains(term) || x.Reason.ToLower().Contains(term));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AuditRecord(x.Id, x.CreatedAt, x.ActorId, x.ActorName, x.InstitutionId,
                x.Action, x.Target, x.Result, x.Reason, x.Before, x.After)).ToListAsync(cancellationToken);
        return new AuditPage(items, total, page, pageSize);
    }
}
