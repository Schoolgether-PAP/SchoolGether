using Microsoft.AspNetCore.Mvc.Filters;

namespace SchoolGether.Api.Platform;

internal sealed class AdministrativeValidationAuditFilter : IAsyncAlwaysRunResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (!context.ModelState.IsValid && AdministrativeRefusalAudit.Applies(context.HttpContext))
            await context.HttpContext.RequestServices.GetRequiredService<AdministrativeRefusalAudit>()
                .RecordAsync(context.HttpContext, "ValidationFailed", context.HttpContext.RequestAborted);
        await next();
    }
}
