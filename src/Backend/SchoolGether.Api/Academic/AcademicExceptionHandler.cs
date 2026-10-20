using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SchoolGether.Application.Academic;
using SchoolGether.Application.Audit;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Academic;

namespace SchoolGether.Api.Academic;

public sealed class AcademicExceptionHandler(IProblemDetailsService problems, IStringLocalizer<ApiMessages> messages) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var code = exception switch { AcademicException x => x.Code, CourseRuleException x => x.Code, _ => null };
        if (code is null) return false;
        await context.RequestServices.GetRequiredService<AcademicRefusalAudit>().RecordAsync(context, code, ct);
        var status = code switch { "InvalidSession" => 401, "Forbidden" => 403, "CourseNotFound" or "CourseUnitNotFound" or "ClassNotFound" => 404,
            "ClassConflict" or "ClassReplayConflict" or "ClassArchived" or "ClassAlreadyActive" or "ClassUnitArchived" or "UnitYearInUse" => 409,
            "CourseUnitConflict" or "CourseUnitReplayConflict" or "CourseUnitArchived" or "CourseUnitAlreadyActive" or "VersionConflict" or "CourseConflict" or "CourseReplayConflict" or "CourseArchived" or "CourseAlreadyActive" => 409, _ => 400 };
        context.Response.StatusCode = status;
        if (status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = new() { Status = status, Title = messages[code] } });
    }
}

internal sealed class AcademicRefusalAudit(ICurrentAccount current, IIdentityStore identities, IAuditWriter audit)
{
    internal async Task RecordAsync(HttpContext context, string reason, CancellationToken ct)
    {
        if (current.UserId is not int id) return;
        var actor = await identities.FindForAuthenticationAsync(id, ct);
        // Alvo constante: não incluir corpo, pesquisa, tokens nem identificadores de outro tenant.
        await audit.AppendIndependentAsync(new(new(id, actor?.Name ?? string.Empty), "academic.request", "academic.course", "refused", reason, actor?.InstitutionId), ct);
    }
}
internal sealed class AcademicValidationAuditFilter : Microsoft.AspNetCore.Mvc.Filters.IAsyncActionFilter, Microsoft.AspNetCore.Mvc.Filters.IOrderedFilter
{
    public int Order => -2100;
    public async Task OnActionExecutionAsync(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context, Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid && context.HttpContext.User.Identity?.IsAuthenticated == true && context.HttpContext.Request.Path.StartsWithSegments("/api/v1/academic"))
            await context.HttpContext.RequestServices.GetRequiredService<AcademicRefusalAudit>().RecordAsync(context.HttpContext, "InvalidCourseInput", context.HttpContext.RequestAborted);
        await next();
    }
}
