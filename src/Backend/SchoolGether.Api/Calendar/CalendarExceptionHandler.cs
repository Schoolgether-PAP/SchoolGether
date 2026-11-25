using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SchoolGether.Domain.Calendar;
namespace SchoolGether.Api.Calendar;

public sealed class CalendarExceptionHandler(IProblemDetailsService problems, IStringLocalizer<CalendarMessages> messages) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not CalendarException e) return false; var status = e.Code switch { "InvalidSession" => 401, "Forbidden" => 403, "NotFound" => 404, "VersionConflict" or "StateConflict" => 409, _ => 400 }; context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = new() { Status = status, Title = messages[e.Code] } });
    }
}
