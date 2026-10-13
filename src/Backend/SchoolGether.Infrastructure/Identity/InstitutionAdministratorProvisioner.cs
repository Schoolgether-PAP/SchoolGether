using System.ComponentModel.DataAnnotations;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Institutions;
using SchoolGether.Domain.Identity;

namespace SchoolGether.Infrastructure.Identity;

internal sealed class InstitutionAdministratorProvisioner(IIdentityStore accounts, IInstitutionDirectory institutions)
    : IInstitutionAdministratorProvisioner
{
    public async Task<Account> CreateAsync(int institutionId, string email, string password, string name, int createdBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !new EmailAddressAttribute().IsValid(email) ||
            string.IsNullOrWhiteSpace(name) || name.Length > 150 || password is null || password.Length is < 12 or > 128)
            throw new IdentityException("InvalidAccount");
        if (!await institutions.ExistsAsync(institutionId, cancellationToken)) throw new IdentityException("InvalidInstitution");
        return await accounts.CreateAsync(new CreateAccount(email, password, name, AccountRole.InstitutionAdmin, institutionId), createdBy, cancellationToken);
    }
}
