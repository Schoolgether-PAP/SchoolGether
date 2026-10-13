using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Institutions;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Institutions;

internal sealed class InstitutionDirectory(SchoolGetherDbContext db) : IInstitutionDirectory
{
    public Task<bool> ExistsAsync(int institutionId, CancellationToken cancellationToken) =>
        db.Institutions.AnyAsync(x => x.Id == institutionId && !x.IsSuspended, cancellationToken);

    public async Task<int> CreateAsync(string name, int createdBy, CancellationToken cancellationToken)
    {
        var institution = new Institution(name, createdBy);
        db.Institutions.Add(institution);
        await db.SaveChangesAsync(cancellationToken);
        return institution.Id;
    }
}
