using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Localization;
using SchoolGether.Contracts.Diagnostics;

namespace SchoolGether.Api.Diagnostics;

[ApiController]
[Route("api/v1/health")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HealthController(
    HealthCheckService healthChecks, IStringLocalizer<ApiMessages> messages) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetLiveness()
    {
        return Ok(new HealthResponse(nameof(HealthStatus.Healthy)));
    }

    [HttpGet("ready")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> GetReadiness(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"), cancellationToken);

        if (report.Status != HealthStatus.Healthy)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: messages["ServiceUnavailable"],
                detail: messages["ReadinessUnavailable"]);
        }

        return Ok(new HealthResponse(nameof(HealthStatus.Healthy)));
    }
}
