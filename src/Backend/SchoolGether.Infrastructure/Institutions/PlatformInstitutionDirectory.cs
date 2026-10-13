using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Institutions;
using SchoolGether.Application.Platform;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Institutions;

internal sealed class PlatformInstitutionDirectory(SchoolGetherDbContext db) : IPlatformInstitutionDirectory
{
    public async Task<PlatformInstitutionPage> ListAsync(int page, int pageSize, string? search, bool? isSuspended, CancellationToken cancellationToken)
    {
        var query = db.Institutions.AsNoTracking();
        if (isSuspended.HasValue) query = query.Where(x => x.IsSuspended == isSuspended.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Id.ToString().Contains(term));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PlatformInstitutionPage(items.Select(Map).ToArray(), total, page, pageSize);
    }

    public async Task<PlatformInstitution?> FindAsync(int id, CancellationToken cancellationToken)
    {
        var institution = await db.Institutions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return institution is null ? null : Map(institution);
    }

    public async Task<PlatformInstitution> CreateAsync(string name, int createdBy, CancellationToken cancellationToken)
    {
        var institution = new Institution(name, createdBy);
        db.Institutions.Add(institution);
        await db.SaveChangesAsync(cancellationToken);
        return Map(institution);
    }

    public async Task<PlatformInstitution> RenameAsync(int id, string name, long rowVersion, CancellationToken cancellationToken)
    {
        var institution = await LoadAsync(id, rowVersion, cancellationToken);
        institution.Rename(name);
        return Map(institution);
    }

    public async Task<PlatformInstitution> SetSuspensionAsync(int id, bool isSuspended, string reason, long rowVersion, CancellationToken cancellationToken)
    {
        var institution = await LoadAsync(id, rowVersion, cancellationToken);
        institution.SetSuspension(isSuspended, reason);
        return Map(institution);
    }

    public async Task<InstitutionStatistics> StatisticsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var total = await db.Institutions.CountAsync(cancellationToken);
        var suspended = await db.Institutions.CountAsync(x => x.IsSuspended, cancellationToken);
        var months = await db.Institutions.Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month })
            .Select(x => new InstitutionMonthlyCount(x.Key.Year, x.Key.Month, x.Count())).ToListAsync(cancellationToken);
        return new InstitutionStatistics(total, total - suspended, suspended, months);
    }

    private async Task<Institution> LoadAsync(int id, long rowVersion, CancellationToken cancellationToken)
    {
        var institution = await db.Institutions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new PlatformException("InstitutionNotFound");
        if (institution.RowVersion != rowVersion) throw new PlatformException("VersionConflict");
        return institution;
    }
    private static PlatformInstitution Map(Institution value) => new(value.Id, value.Name, value.IsSuspended,
        value.SuspensionReason, DateTime.SpecifyKind(value.CreatedAt, DateTimeKind.Utc), 0, value.RowVersion);
}
