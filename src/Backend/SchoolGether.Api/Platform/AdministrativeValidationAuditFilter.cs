using Microsoft.AspNetCore.Mvc.Filters;

namespace SchoolGether.Api.Platform;

internal sealed class AdministrativeValidationAuditFilter(AdministrativeRefusalAudit audit) : IAsyncAlwaysRunResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
            await audit.RecordAsync(context.HttpContext, "ValidationFailed", context.HttpContext.RequestAborted);
        await next();
    }
}
