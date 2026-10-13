using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using SchoolGether.Application.Platform;
using SchoolGether.Contracts.Platform;

namespace SchoolGether.Api.Platform;

[ApiController, Authorize]
[Route("api/v1/platform/dashboard")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformDashboardController(DashboardService dashboard, IStringLocalizer<ApiMessages> messages) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PlatformDashboardResponse>(StatusCodes.Status200OK)]
    public async Task<PlatformDashboardResponse> Get(CancellationToken cancellationToken)
    {
        var result = await dashboard.GetAsync(cancellationToken);
        return new PlatformDashboardResponse(result.InstitutionsTotal, result.ActiveInstitutions, result.SuspendedInstitutions,
            result.AccountsTotal, result.InstitutionsTrend.Select(x => new SchoolGether.Contracts.Platform.InstitutionTrendPoint(x.Label + (x.IsPartial ? " (" + messages["DashboardPartialMonth"] + ")" : ""), x.Count)).ToArray(),
            result.RecentActions.Select(x => new AuditResponse(x.Id, DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc), x.ActorId, x.ActorName,
                x.InstitutionId, x.Action, x.Target, x.Result, x.Reason, Parse(x.Before), Parse(x.After))).ToArray());
    }
    [HttpGet("preferences")]
    [ProducesResponseType<DashboardPreferenceResponse>(StatusCodes.Status200OK)]
    public async Task<DashboardPreferenceResponse> Preferences(CancellationToken cancellationToken) =>
        Map(await dashboard.GetPreferencesAsync(cancellationToken));
    [HttpPut("preferences")]
    [ProducesResponseType<DashboardPreferenceResponse>(StatusCodes.Status200OK)]
    public async Task<DashboardPreferenceResponse> SavePreferences(SaveDashboardPreferenceRequest request, CancellationToken cancellationToken) =>
        Map(await dashboard.SavePreferencesAsync(new DashboardPreference(request.SchemaVersion, request.RowVersion,
            request.Widgets.Select(x => x is null ? null! : new SchoolGether.Application.Platform.DashboardWidget(x.Id, x.Size, x.Visible)).ToArray()), cancellationToken));
    private static DashboardPreferenceResponse Map(DashboardPreference value) => new(value.SchemaVersion, value.RowVersion,
        value.Widgets.Select(x => new SchoolGether.Contracts.Platform.DashboardWidget(x.Id, x.Size, x.Visible)).ToArray());
    private static JsonElement? Parse(string? value) => value is null ? null : JsonSerializer.Deserialize<JsonElement>(value);
}
