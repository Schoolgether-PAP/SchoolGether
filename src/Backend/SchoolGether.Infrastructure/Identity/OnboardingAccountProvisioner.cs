using Microsoft.AspNetCore.Identity;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Onboarding;
using SchoolGether.Domain.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Identity;

internal sealed class OnboardingAccountProvisioner(UserManager<ApplicationUser> users, SchoolGetherDbContext db)
    : IOnboardingAccountProvisioner
{
    public async Task<RegisteredOwner> CreateOwnerAsync(string name, string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scope = db.OnboardingScope ?? throw new InvalidOperationException("OnboardingTransactionRequired");
        var owner = new ApplicationUser { UserName = email, Email = email, Name = name, LockoutEnabled = true };
        scope.BindOwner(owner);
        var result = await users.CreateAsync(owner, password);
        if (!result.Succeeded)
            throw new OnboardingException(result.Errors.Any(x => x.Code.StartsWith("Duplicate", StringComparison.Ordinal))
                ? "SignupEmailUnavailable" : "InvalidSignup");
        owner.AssociateOwnCreator();
        await db.SaveChangesAsync(cancellationToken);
        return new RegisteredOwner(owner.Id, owner.Name, owner.Email!);
    }
    public async Task LinkInstitutionAsync(int ownerId, int institutionId, CancellationToken cancellationToken)
    {
        var scope = db.OnboardingScope ?? throw new InvalidOperationException("OnboardingTransactionRequired");
        var owner = scope.RequireOwner(ownerId);
        var member = new InstitutionMember(ownerId, institutionId, owner.Name, AccountRole.InstitutionAdmin, ownerId);
        scope.BindMember(member);
        db.Members.Add(member);
        await db.SaveChangesAsync(cancellationToken);
    }
}
