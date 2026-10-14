using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Localization;
using SchoolGether.Application.Onboarding;
using SchoolGether.Application.Billing;

namespace SchoolGether.Api.Onboarding;

public sealed class OnboardingExceptionHandler(IProblemDetailsService problems, IStringLocalizer<ApiMessages> messages) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var code = exception switch { OnboardingException failure => failure.Code, BillingException failure => failure.Code, _ => null };
        if (code is null) return false;
        var status = code switch {
            "SignupAuthenticated" => StatusCodes.Status403Forbidden,
            "SignupDisabled" => StatusCodes.Status503ServiceUnavailable,
            "SignupEmailUnavailable" or "OutdatedCatalog" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest };
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context,
            ProblemDetails = new() { Status = status, Title = messages[code] } });
    }
}
