using SchoolGether.Application.Audit;
using SchoolGether.Application.Identity;

namespace SchoolGether.Api.Platform;

internal sealed class AdministrativeRefusalAudit(ICurrentAccount current, IIdentityStore accounts, IAuditWriter audit)
{
    internal static bool Applies(HttpContext context) => context.User.Identity?.IsAuthenticated == true &&
        (context.Request.Path.StartsWithSegments("/api/v1/platform") ||
         context.Request.Path.StartsWithSegments("/api/v1/institutions") ||
         context.Request.Path.StartsWithSegments("/api/v1/identity/users") ||
         context.Request.Path.StartsWithSegments("/api/v1/support"));

    internal async Task RecordAsync(HttpContext context, string reason, CancellationToken cancellationToken)
    {
        if (!Applies(context) || current.UserId is not int id) return;
        var support = context.Request.Path.StartsWithSegments("/api/v1/support");
        if (support && reason is not ("Forbidden" or "InvalidSession" or "SupportNotFound")) return;
        var actor = await accounts.FindForAuthenticationAsync(id, cancellationToken);
        await audit.AppendIndependentAsync(new AuditEvent(new AuditActor(id, actor?.Name ?? string.Empty),
            support ? "support.request" : "platform.request", support ? SupportTarget(context) : context.Request.Path.Value ?? "platform", "refused", reason,
            actor?.InstitutionId), cancellationToken);
    }

    private static string SupportTarget(HttpContext context)
    {
        // O middleware de exceções limpa RouteValues. Extrair só tipo/ID pedido,
        // sem consultar o recurso oculto nem guardar URL/query.
        var segments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is { Length: >= 5 } && int.TryParse(segments[4], out var id))
        {
            if (segments[3].Equals("attachments", StringComparison.OrdinalIgnoreCase)) return "support.attachment:" + id;
            if (segments[3].Equals("tickets", StringComparison.OrdinalIgnoreCase)) return "support.ticket:" + id;
        }
        return "support";
    }
}
