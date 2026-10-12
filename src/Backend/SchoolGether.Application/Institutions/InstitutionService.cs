using SchoolGether.Application.Identity;

namespace SchoolGether.Application.Institutions;

public sealed class InstitutionService(IdentityService identity, ICurrentAccount current, IInstitutionDirectory institutions)
{
    public async Task<int> CreateAsync(string name, CancellationToken cancellationToken)
    {
        await identity.GetMeAsync(cancellationToken);
        if (current.Role != "PlatformAdmin") throw new IdentityException("Forbidden");
        return await institutions.CreateAsync(name, current.UserId!.Value, cancellationToken);
    }
}
