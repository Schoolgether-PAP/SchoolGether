using SchoolGether.Application.Onboarding;

namespace SchoolGether.Application.Identity;

public interface IOnboardingAccountProvisioner
{
    Task<RegisteredOwner> CreateOwnerAsync(string name, string email, string password, CancellationToken cancellationToken);
    Task LinkInstitutionAsync(int ownerId, int institutionId, CancellationToken cancellationToken);
}
