using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SchoolGether.Application.Platform;

namespace SchoolGether.Api.Platform;

public sealed class PlatformExceptionHandler(IProblemDetailsService problems, IStringLocalizer<ApiMessages> messages) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not PlatformException failure) return false;
        var status = failure.Code switch {
            "InvalidSession" => StatusCodes.Status401Unauthorized,
            "Forbidden" or "MasterProtected" => StatusCodes.Status403Forbidden,
            "PlatformAdminNotFound" or "InstitutionNotFound" => StatusCodes.Status404NotFound,
            "AccountExists" or "VersionConflict" or "MasterExists" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest };
        context.Response.StatusCode = status;
        if (status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = messages[failure.Code] } });
    }
}
