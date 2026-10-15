using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Institutions;
using SchoolGether.Application.Platform;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Institutions;

internal sealed class InstitutionSetupStore(SchoolGetherDbContext db) : IInstitutionSetupStore
{
    public async Task<InstitutionSetup> GetAsync(int institutionId, CancellationToken cancellationToken)
    {
        var institution = await db.Institutions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == institutionId, cancellationToken)
            ?? throw new PlatformException("InstitutionNotFound");
        var profile = await db.InstitutionProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == institutionId, cancellationToken);
        return Map(institution, profile);
    }

    public async Task<InstitutionSetup> SaveAsync(int institutionId, int actorId, SaveInstitutionSetup command, DateTime completedAt,
        CancellationToken cancellationToken)
    {
        var institution = await db.Institutions.SingleOrDefaultAsync(x => x.Id == institutionId, cancellationToken)
            ?? throw new PlatformException("InstitutionNotFound");
        if (institution.RowVersion != command.RowVersion) throw new PlatformException("VersionConflict");
        var profile = await db.InstitutionProfiles.SingleOrDefaultAsync(x => x.InstitutionId == institutionId, cancellationToken);
        if (profile is null)
        {
            profile = new InstitutionProfile(institutionId, command.InstitutionType, command.Locality, actorId);
            db.InstitutionProfiles.Add(profile);
        }
        institution.SaveSetupName(command.Name);
        profile.Complete(command.InstitutionType, command.Locality, command.AcademicYear, completedAt);
        return Map(institution, profile);
    }

    private static InstitutionSetup Map(Institution institution, InstitutionProfile? profile) => new(institution.Id, institution.Name,
        profile?.InstitutionType ?? "Other", profile?.Locality, profile?.AcademicYear, profile?.IsComplete ?? false, institution.RowVersion);
}
