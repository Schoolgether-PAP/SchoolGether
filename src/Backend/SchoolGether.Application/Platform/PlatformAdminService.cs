using SchoolGether.Application.Audit;

namespace SchoolGether.Application.Platform;

public sealed class PlatformAdminService(PlatformAuthorization authorization, IPlatformAdminDirectory directory,
    IAuditWriter audit, IAuditReader history, IAdministrativeUnitOfWork unitOfWork)
{
    public Task<PlatformAdmin> ContextAsync(CancellationToken cancellationToken) => authorization.GetContextAsync(cancellationToken);

    public async Task<PlatformAdminPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var actor = await authorization.RequireMasterAsync(cancellationToken);
        await ValidateReadAsync(actor, "platform.admin.list", "admins", page, pageSize, null, cancellationToken);
        var result = await directory.ListAsync(page, pageSize, cancellationToken);
        await audit.AppendIndependentAsync(Event(actor, "platform.admin.list", "admins", "Consulta administrativa"), cancellationToken);
        return result;
    }

    public async Task<PlatformAdmin> CreateAsync(CreatePlatformAdmin request, CancellationToken cancellationToken)
    {
        var actor = await authorization.RequireMasterAsync(cancellationToken);
        return await MutateAsync(actor, "platform.admin.create", "admins", async token =>
        {
            request = request with { Permissions = PlatformCapabilities.Normalize(request.Permissions) };
            var created = await directory.CreateAsync(request, actor.Id, token);
            audit.Append(Event(actor, "platform.admin.create", created.Id.ToString(), "Criação de administrador", after: Snapshot(created)));
            return created;
        }, cancellationToken);
    }

    public async Task<PlatformAdmin> UpdateAsync(int id, UpdatePlatformAdmin request, CancellationToken cancellationToken)
    {
        var actor = await authorization.RequireMasterAsync(cancellationToken);
        return await MutateAsync(actor, "platform.admin.update", id.ToString(), async token =>
        {
            var before = await directory.FindAsync(id, token) ?? throw new PlatformException("PlatformAdminNotFound");
            if (before.IsMaster) throw new PlatformException("MasterProtected");
            request = request with { Permissions = PlatformCapabilities.Normalize(request.Permissions) };
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 150 || string.IsNullOrWhiteSpace(request.Reason) ||
                request.Reason.Length > 500 || request.RowVersion < 1) throw new PlatformException("InvalidAccount");
            var updated = await directory.UpdateAsync(id, request, token);
            audit.Append(Event(actor, "platform.admin.update", id.ToString(), request.Reason.Trim(), before: Snapshot(before), after: Snapshot(updated)));
            return updated;
        }, cancellationToken);
    }

    public async Task<AuditPage> AuditAsync(int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        var actor = await authorization.RequireAsync(PlatformCapabilities.AuditRead, cancellationToken);
        await ValidateReadAsync(actor, "platform.audit.list", "audit", page, pageSize, search, cancellationToken);
        var result = await history.ListAsync(page, pageSize, search, cancellationToken);
        // Uma consulta produz um só evento, escrito depois da leitura; não consulta o próprio evento.
        await audit.AppendIndependentAsync(Event(actor, "platform.audit.list", "audit", "Consulta do histórico"), cancellationToken);
        return result;
    }

    private async Task<PlatformAdmin> MutateAsync(PlatformAdmin actor, string action, string target,
        Func<CancellationToken, Task<PlatformAdmin>> mutation, CancellationToken cancellationToken)
    {
        try { return await unitOfWork.ExecuteAsync(mutation, cancellationToken); }
        catch (PlatformException exception)
        {
            await audit.AppendIndependentAsync(Event(actor, action, target, exception.Code, "refused"), cancellationToken);
            throw;
        }
    }

    private static AuditEvent Event(PlatformAdmin actor, string action, string target, string reason, string result = "success",
        IReadOnlyDictionary<string, object?>? before = null, IReadOnlyDictionary<string, object?>? after = null) =>
        new(new AuditActor(actor.Id, actor.Name), action, target, result, reason, Before: before, After: after);

    private static Dictionary<string, object?> Snapshot(PlatformAdmin value) => new() {
        ["id"] = value.Id, ["name"] = value.Name, ["isMaster"] = value.IsMaster,
        ["isActive"] = value.IsActive, ["permissions"] = value.Permissions, ["rowVersion"] = value.RowVersion };

    private async Task ValidateReadAsync(PlatformAdmin actor, string action, string target, int page, int pageSize,
        string? search, CancellationToken cancellationToken)
    {
        try
        {
            ValidatePagination(page, pageSize);
            if (search?.Length > 150) throw new PlatformException("InvalidSearch");
        }
        catch (PlatformException exception)
        {
            await audit.AppendIndependentAsync(Event(actor, action, target, exception.Code, "refused"), cancellationToken);
            throw;
        }
    }

    public static void ValidatePagination(int page, int pageSize)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100) throw new PlatformException("InvalidPagination");
    }
}
