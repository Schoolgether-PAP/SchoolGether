using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SchoolGether.Application.Identity;

namespace SchoolGether.Api.Identity;

public sealed class IdentityExceptionHandler(IProblemDetailsService problems, IStringLocalizer<ApiMessages> messages) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not IdentityException failure) return false;
        var status = failure.Code switch {
            "InvalidCredentials" or "InvalidSession" => StatusCodes.Status401Unauthorized,
            "Forbidden" => StatusCodes.Status403Forbidden,
            "AccountExists" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest };
        context.Response.StatusCode = status;
        if (status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = messages[failure.Code] } });
    }
}
