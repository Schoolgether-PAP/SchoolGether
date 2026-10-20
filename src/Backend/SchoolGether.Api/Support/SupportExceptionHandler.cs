using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SchoolGether.Application.Support;
using SchoolGether.Domain.Support;
using SchoolGether.Api.Platform;

namespace SchoolGether.Api.Support;

public sealed class SupportExceptionHandler(IProblemDetailsService problems, IStringLocalizer<ApiMessages> messages) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var code = exception switch { SupportException ex => ex.Code, SupportRuleException ex => ex.Code, _ => null };
        if (code is null) return false;
        await context.RequestServices.GetRequiredService<AdministrativeRefusalAudit>().RecordAsync(context, code, ct);
        var status = code switch {
            "InvalidSession" => 401, "Forbidden" => 403, "SupportNotFound" => 404,
            "VersionConflict" or "SupportReplayConflict" or "TicketClosed" or "SupportStorageQuotaExceeded" => 409, "SupportDisabled" or "SupportFileUnavailable" => 503, "SupportRequestTooLarge" => 413, _ => 400 };
        context.Response.StatusCode = status;
        if (status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
        var resource = status == 400 ? "InvalidSupportInput" : code;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = messages[resource] } });
    }
}
