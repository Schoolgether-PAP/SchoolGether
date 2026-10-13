using SchoolGether.Application.Audit;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Platform;

namespace SchoolGether.Application.Institutions;

public sealed class PlatformInstitutionService(PlatformAuthorization authorization, IPlatformInstitutionDirectory directory,
    IPlatformAccountReader accounts, IInstitutionAdministratorProvisioner administrators, IAuditWriter audit, IAdministrativeUnitOfWork unitOfWork)
{
    public async Task<PlatformInstitutionPage> ListAsync(int page, int pageSize, string? search, bool? isSuspended, CancellationToken cancellationToken)
    {
        var actor = await authorization.RequireAsync(PlatformCapabilities.InstitutionsRead, cancellationToken);
        try
        {
            PlatformAdminService.ValidatePagination(page, pageSize);
            if (search?.Length > 150) throw new PlatformException("InvalidSearch");
        }
        catch (PlatformException exception)
        {
            await audit.AppendIndependentAsync(Event(actor, "institution.list", "institutions", exception.Code, result: "refused"), cancellationToken);
            throw;
        }
        var result = await directory.ListAsync(page, pageSize, search, isSuspended, cancellationToken);
        var counts = await accounts.CountByInstitutionsAsync(result.Items.Select(x => x.Id).ToArray(), cancellationToken);
        await audit.AppendIndependentAsync(Event(actor, "institution.list", "institutions", "Consulta de instituições"), cancellationToken);
        return result with { Items = result.Items.Select(x => x with { AccountsCount = counts.GetValueOrDefault(x.Id) }).ToArray() };
    }

    public async Task<PlatformInstitutionDetail> DetailAsync(int id, CancellationToken cancellationToken)
    {
        var actor = await authorization.RequireAsync(PlatformCapabilities.InstitutionsRead, cancellationToken);
        try
        {
            var institution = await FindWithCountAsync(id, cancellationToken);
            var responsible = await accounts.ListAdministratorsAsync(id, cancellationToken);
            await audit.AppendIndependentAsync(Event(actor, "institution.read", id.ToString(), "Consulta de responsáveis", id), cancellationToken);
            return new PlatformInstitutionDetail(institution, responsible);
        }
        catch (PlatformException exception)
        {
            await audit.AppendIndependentAsync(Event(actor, "institution.read", id.ToString(), exception.Code, id, "refused"), cancellationToken);
            throw;
        }
    }

    public async Task<PlatformInstitution> CreateAsync(CreatePlatformInstitution request, CancellationToken cancellationToken)
    {
        var actor = await authorization.RequireAsync(PlatformCapabilities.InstitutionsWrite, cancellationToken);
        return await MutateAsync(actor, "institution.create", null, async token =>
        {
            ValidateName(request.Name); ValidateReason(request.Reason);
            var created = await directory.CreateAsync(request.Name.Trim(), actor.Id, token);
            audit.Append(Event(actor, "institution.create", created.Id.ToString(), request.Reason.Trim(), created.Id, after: Snapshot(created)));
            return created;
        }, cancellationToken);
    }

    public async Task<PlatformInstitution> UpdateAsync(int id, UpdatePlatformInstitution request, CancellationToken cancellationToken)
    {
        var actor = await authorization.RequireAsync(PlatformCapabilities.InstitutionsWrite, cancellationToken);
        return await MutateAsync(actor, "institution.update", id, async token =>
        {
            ValidateName(request.Name); ValidateReason(request.Reason); ValidateVersion(request.RowVersion);
            var before = await FindWithCountAsync(id, token);
            var updated = await directory.RenameAsync(id, request.Name.Trim(), request.RowVersion, token);
            updated = updated with { AccountsCount = before.AccountsCount };
            audit.Append(Event(actor, "institution.update", id.ToString(), request.Reason.Trim(), id, before: Snapshot(before), after: Snapshot(updated)));
            return updated;
        }, cancellationToken);
    }

    public async Task<PlatformInstitution> SetSuspensionAsync(int id, SetInstitutionSuspension request, CancellationToken cancellationToken)
    {
        var actor = await authorization.RequireAsync(PlatformCapabilities.InstitutionsSuspend, cancellationToken);
        return await MutateAsync(actor, "institution.suspension", id, async token =>
        {
            ValidateReason(request.Reason); ValidateVersion(request.RowVersion);
            var before = await FindWithCountAsync(id, token);
            var updated = await directory.SetSuspensionAsync(id, request.IsSuspended, request.Reason.Trim(), request.RowVersion, token);
            updated = updated with { AccountsCount = before.AccountsCount };
            audit.Append(Event(actor, "institution.suspension", id.ToString(), request.Reason.Trim(), id, before: Snapshot(before), after: Snapshot(updated)));
            return updated;
        }, cancellationToken);
    }

    public async Task<Account> CreateAdministratorAsync(int id, CreateInstitutionAdministrator request, CancellationToken cancellationToken)
    {
        var actor = await authorization.RequireAsync(PlatformCapabilities.InstitutionsWrite, cancellationToken);
        return await MutateAsync(actor, "institution.administrator.create", id, async token =>
        {
            ValidateReason(request.Reason);
            var institution = await directory.FindAsync(id, token) ?? throw new PlatformException("InstitutionNotFound");
            if (institution.IsSuspended) throw new PlatformException("InvalidInstitution");
            var created = await administrators.CreateAsync(id, request.Email, request.Password, request.Name, actor.Id, token);
            audit.Append(Event(actor, "institution.administrator.create", created.Id.ToString(), request.Reason.Trim(), id,
                after: new Dictionary<string, object?> { ["id"] = created.Id, ["name"] = created.Name,
                    ["role"] = created.Role.ToString(), ["institutionId"] = id }));
            return created;
        }, cancellationToken);
    }

    private async Task<PlatformInstitution> FindWithCountAsync(int id, CancellationToken cancellationToken)
    {
        var institution = await directory.FindAsync(id, cancellationToken) ?? throw new PlatformException("InstitutionNotFound");
        var counts = await accounts.CountByInstitutionsAsync([id], cancellationToken);
        return institution with { AccountsCount = counts.GetValueOrDefault(id) };
    }

    private async Task<T> MutateAsync<T>(PlatformAdmin actor, string action, int? institutionId,
        Func<CancellationToken, Task<T>> mutation, CancellationToken cancellationToken)
    {
        try { return await unitOfWork.ExecuteAsync(mutation, cancellationToken); }
        catch (PlatformException exception)
        {
            await audit.AppendIndependentAsync(Event(actor, action, institutionId?.ToString() ?? "institutions", exception.Code, institutionId, "refused"), cancellationToken);
            throw;
        }
        // Identity conserva os seus erros e o handler HTTP regista a recusa, depois do rollback.
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new PlatformException("InvalidInstitutionName");
    }
    internal static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) throw new PlatformException("InvalidReason");
    }
    private static void ValidateVersion(long version)
    {
        if (version < 1) throw new PlatformException("InvalidVersion");
    }
    private static AuditEvent Event(PlatformAdmin actor, string action, string target, string reason, int? institutionId = null,
        string result = "success", IReadOnlyDictionary<string, object?>? before = null, IReadOnlyDictionary<string, object?>? after = null) =>
        new(new AuditActor(actor.Id, actor.Name), action, target, result, reason, institutionId, before, after);
    private static Dictionary<string, object?> Snapshot(PlatformInstitution value) => new() {
        ["id"] = value.Id, ["name"] = value.Name, ["isSuspended"] = value.IsSuspended,
        ["suspensionReason"] = value.SuspensionReason, ["rowVersion"] = value.RowVersion };
}
