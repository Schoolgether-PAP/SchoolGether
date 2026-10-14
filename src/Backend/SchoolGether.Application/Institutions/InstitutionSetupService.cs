using System.Globalization;
using SchoolGether.Application.Audit;
using SchoolGether.Application.Billing;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Platform;
using SchoolGether.Domain.Identity;
using SchoolGether.Domain.Institutions;

namespace SchoolGether.Application.Institutions;

public sealed class InstitutionSetupService(IdentityService identity, IInstitutionSetupStore store,
    ISubscriptionStore subscriptions, IAdministrativeUnitOfWork unitOfWork, IAuditWriter audit, TimeProvider clock)
{
    public async Task<InstitutionSetup> GetAsync(CancellationToken cancellationToken)
    {
        var actor = await RequireAdministratorAsync(cancellationToken);
        return WithSuggestion(await store.GetAsync(actor.InstitutionId!.Value, cancellationToken));
    }

    public async Task<InstitutionSetup> SaveAsync(SaveInstitutionSetup command, CancellationToken cancellationToken)
    {
        var actor = await RequireAdministratorAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 200 ||
            !InstitutionProfile.IsValidType(command.InstitutionType) || command.Locality?.Length > 100 ||
            !InstitutionProfile.IsValidAcademicYear(command.AcademicYear)) throw new PlatformException("InvalidInstitutionSetup");
        if (command.RowVersion < 1) throw new PlatformException("InvalidVersion");
        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var result = await store.SaveAsync(actor.InstitutionId!.Value, actor.Id, command, clock.GetUtcNow().UtcDateTime, token);
                audit.Append(new AuditEvent(new AuditActor(actor.Id, actor.Name), "institution.setup.complete", actor.InstitutionId.Value.ToString(CultureInfo.InvariantCulture),
                    "success", "Configuração institucional", actor.InstitutionId,
                    After: new Dictionary<string, object?> { ["institutionId"] = result.InstitutionId, ["name"] = result.Name, ["rowVersion"] = result.RowVersion }));
                return WithSuggestion(result);
            }, cancellationToken);
        }
        // The only unique insert in this operation is the institution's one-to-one profile.
        catch (PlatformException exception) when (exception.Code == "AccountExists") { throw new PlatformException("VersionConflict"); }
    }

    public async Task<InstitutionSubscription> GetSubscriptionAsync(CancellationToken cancellationToken)
    {
        var actor = await RequireAdministratorAsync(cancellationToken);
        return await subscriptions.FindForInstitutionAsync(actor.InstitutionId!.Value, cancellationToken)
            ?? new InstitutionSubscription("None", null, null, (await identity.ListMembersAsync(1, 1, cancellationToken)).Total);
    }

    private async Task<Account> RequireAdministratorAsync(CancellationToken cancellationToken)
    {
        var actor = await identity.GetMeAsync(cancellationToken);
        if (actor.Role != AccountRole.InstitutionAdmin || actor.InstitutionId is null) throw new IdentityException("Forbidden");
        return actor;
    }
    private InstitutionSetup WithSuggestion(InstitutionSetup setup)
    {
        var now = clock.GetUtcNow();
        var start = now.Month >= 9 ? now.Year : now.Year - 1;
        return setup with { SuggestedAcademicYear = string.Create(CultureInfo.InvariantCulture, $"{start:D4}/{start + 1:D4}") };
    }
}
