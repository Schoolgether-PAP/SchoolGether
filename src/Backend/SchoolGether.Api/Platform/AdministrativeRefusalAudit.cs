using SchoolGether.Application.Audit;
using SchoolGether.Application.Identity;

namespace SchoolGether.Api.Platform;

internal sealed class AdministrativeRefusalAudit(ICurrentAccount current, IIdentityStore accounts, IAuditWriter audit)
{
    internal static bool Applies(HttpContext context) => context.User.Identity?.IsAuthenticated == true &&
        (context.Request.Path.StartsWithSegments("/api/v1/platform") ||
         context.Request.Path.StartsWithSegments("/api/v1/institutions") ||
         context.Request.Path.StartsWithSegments("/api/v1/identity/users"));

    internal async Task RecordAsync(HttpContext context, string reason, CancellationToken cancellationToken)
    {
        if (!Applies(context) || current.UserId is not int id) return;
        var actor = await accounts.FindForAuthenticationAsync(id, cancellationToken);
        await audit.AppendIndependentAsync(new AuditEvent(new AuditActor(id, actor?.Name ?? string.Empty),
            "platform.request", context.Request.Path.Value ?? "platform", "refused", reason,
            actor?.InstitutionId), cancellationToken);
    }
}
