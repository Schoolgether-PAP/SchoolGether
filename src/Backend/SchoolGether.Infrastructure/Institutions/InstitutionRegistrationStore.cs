using SchoolGether.Application.Institutions;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Institutions;

internal sealed class InstitutionRegistrationStore(SchoolGetherDbContext db) : IInstitutionRegistrationStore
{
    public async Task<int> CreateAsync(string name, string type, string? locality, string academicYear, int ownerId, CancellationToken cancellationToken, InstitutionContactDetails? details = null)
    {
        var scope = db.OnboardingScope ?? throw new InvalidOperationException("OnboardingTransactionRequired");
        var institution = new Institution(name, ownerId);
        scope.BindInstitution(institution);
        db.Institutions.Add(institution);
        await db.SaveChangesAsync(cancellationToken);
        var profile = new InstitutionProfile(institution.Id, type, locality, ownerId);
        profile.Complete(type, locality, academicYear, DateTime.UtcNow);
        if (details is not null) profile.SetContactDetails(details);
        scope.BindProfile(profile);
        db.InstitutionProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        return institution.Id;
    }
}
