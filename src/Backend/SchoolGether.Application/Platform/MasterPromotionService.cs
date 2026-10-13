using SchoolGether.Application.Audit;

namespace SchoolGether.Application.Platform;

// Só consumido pelo assistente local; não existe rota HTTP para promover master.
public sealed class MasterPromotionService(IPlatformAdminDirectory directory, IAdministrativeUnitOfWork unitOfWork, IAuditWriter audit)
{
    public async Task PromoteAsync(int accountId, int confirmationId, CancellationToken cancellationToken)
    {
        var candidate = accountId > 0 ? await directory.FindAsync(accountId, cancellationToken) : null;
        var actor = new AuditActor(candidate?.Id ?? 0, candidate?.Name ?? "Operador local");
        try
        {
            if (accountId < 1 || accountId != confirmationId) throw new PlatformException("MasterConfirmationMismatch");
            await unitOfWork.ExecuteAsync(async token =>
            {
                var master = await directory.PromoteMasterAsync(accountId, token);
                audit.Append(new AuditEvent(actor, "platform.master.promote", accountId.ToString(), "success",
                    "Promoção local confirmada pelo operador", Before: new Dictionary<string, object?> { ["isMaster"] = false },
                    After: new Dictionary<string, object?> { ["isMaster"] = true, ["id"] = master.Id }, RequestReference: "local:promote-master"));
                return master;
            }, cancellationToken);
        }
        catch (PlatformException exception)
        {
            await audit.AppendIndependentAsync(new AuditEvent(actor, "platform.master.promote", accountId.ToString(), "refused",
                exception.Code, RequestReference: "local:promote-master"), cancellationToken);
            throw;
        }
    }
}
