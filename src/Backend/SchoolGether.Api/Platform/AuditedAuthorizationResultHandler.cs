using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace SchoolGether.Api.Platform;

internal sealed class AuditedAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        if (result.Forbidden)
            await context.RequestServices.GetRequiredService<AdministrativeRefusalAudit>()
                .RecordAsync(context, "Forbidden", context.RequestAborted);
        await fallback.HandleAsync(next, context, policy, result);
    }
}
