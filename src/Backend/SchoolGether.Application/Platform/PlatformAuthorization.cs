using SchoolGether.Application.Audit;
using SchoolGether.Application.Identity;

namespace SchoolGether.Application.Platform;

public sealed class PlatformAuthorization(ICurrentAccount current, IPlatformAdminDirectory directory, IAuditWriter audit)
{
    public async Task<PlatformAdmin> RequireAsync(string permission, CancellationToken cancellationToken)
    {
        var actor = await GetContextAsync(cancellationToken);
        if (!PlatformCapabilities.All.Contains(permission) || (!actor.IsMaster && !actor.Permissions.Contains(permission)))
        {
            await RefuseAsync(actor, permission, cancellationToken);
            throw new PlatformException("Forbidden");
        }
        return actor;
    }

    public async Task<PlatformAdmin> RequireMasterAsync(CancellationToken cancellationToken)
    {
        var actor = await GetContextAsync(cancellationToken);
        if (!actor.IsMaster)
        {
            await RefuseAsync(actor, "platform.admin.manage", cancellationToken);
            throw new PlatformException("Forbidden");
        }
        return actor;
    }

    public async Task<PlatformAdmin> GetContextAsync(CancellationToken cancellationToken)
    {
        var actor = current.UserId is int id ? await directory.FindAsync(id, cancellationToken) : null;
        if (actor is null || current.Role != "PlatformAdmin" || current.InstitutionId is not null)
        {
            await audit.AppendIndependentAsync(new AuditEvent(new AuditActor(current.UserId ?? 0, string.Empty),
                "platform.access", "platform", "refused", "Forbidden"), cancellationToken);
            throw new PlatformException("Forbidden");
        }
        if (!actor.IsActive)
        {
            await RefuseAsync(actor, "platform.access", cancellationToken);
            throw new PlatformException("InvalidSession");
        }
        return actor with { Permissions = actor.IsMaster ? PlatformCapabilities.All.ToArray() : actor.Permissions };
    }

    private Task RefuseAsync(PlatformAdmin actor, string action, CancellationToken cancellationToken) =>
        audit.AppendIndependentAsync(new AuditEvent(new AuditActor(actor.Id, actor.Name), action,
            "platform", "refused", "Forbidden"), cancellationToken);
}
