using System.ComponentModel.DataAnnotations;
using SchoolGether.Application.Audit;
using SchoolGether.Application.Billing;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Institutions;
using SchoolGether.Domain.Institutions;

namespace SchoolGether.Application.Onboarding;

public sealed class OnboardingService(PlanCatalog catalog, IOnboardingAccountProvisioner accounts,
    IInstitutionRegistrationStore institutions, ISubscriptionStore subscriptions, IOnboardingUnitOfWork unitOfWork,
    IAuditWriter audit, ICurrentAccount current, OnboardingOptions options)
{
    public Task<Registration> RegisterAsync(RegisterInstitution command, CancellationToken cancellationToken)
    {
        if (current.UserId.HasValue) throw new OnboardingException("SignupAuthenticated");
        if (!options.Enabled) throw new OnboardingException("SignupDisabled");
        if (!command.AcceptDemonstration || string.IsNullOrWhiteSpace(command.OwnerName) || command.OwnerName.Trim().Length > 150 ||
            string.IsNullOrWhiteSpace(command.Email) || command.Email.Trim().Length > 256 || !new EmailAddressAttribute().IsValid(command.Email.Trim()) ||
            command.Password is null || command.Password.Length is < 12 or > 128 ||
            string.IsNullOrWhiteSpace(command.InstitutionName) || command.InstitutionName.Trim().Length > 200 ||
            !InstitutionProfile.IsValidType(command.InstitutionType) || command.Locality?.Trim().Length > 100)
            throw new OnboardingException("InvalidSignup");
        var plan = catalog.RequirePlan(command.PlanId, command.CatalogVersion);
        return unitOfWork.ExecuteAsync(async token =>
        {
            var owner = await accounts.CreateOwnerAsync(command.OwnerName.Trim(), command.Email.Trim(), command.Password, token);
            var institutionId = await institutions.CreateAsync(command.InstitutionName.Trim(), command.InstitutionType, command.Locality?.Trim(), owner.Id, token);
            await accounts.LinkInstitutionAsync(owner.Id, institutionId, token);
            await subscriptions.CreateAsync(institutionId, plan, catalog.Version, owner.Id, token);
            audit.Append(new AuditEvent(new AuditActor(owner.Id, "Auto-serviço"), "onboarding.institution.create",
                institutionId.ToString(), "success", "Adesão pública de demonstração", institutionId,
                After: new Dictionary<string, object?> { ["id"] = owner.Id, ["institutionId"] = institutionId,
                    ["name"] = command.InstitutionName.Trim(), ["planId"] = plan.Id,
                    ["catalogVersion"] = catalog.Version, ["subscriptionStatus"] = "Demonstration" }));
            return new Registration(institutionId, owner.Id, owner.Email);
        }, cancellationToken);
    }
}
