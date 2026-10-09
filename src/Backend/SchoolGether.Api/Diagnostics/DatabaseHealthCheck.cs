using Microsoft.Extensions.Diagnostics.HealthChecks;
using SchoolGether.Application.Diagnostics;

namespace SchoolGether.Api.Diagnostics;

public sealed class DatabaseHealthCheck(IDatabaseReadiness databaseReadiness) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return await databaseReadiness.IsReadyAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}
