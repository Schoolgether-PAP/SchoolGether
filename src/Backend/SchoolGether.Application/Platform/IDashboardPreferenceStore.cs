using SchoolGether.Application.Audit;

namespace SchoolGether.Application.Platform;

public sealed record DashboardWidget(string Id, string Size, bool Visible);
public sealed record DashboardPreference(int SchemaVersion, long RowVersion, IReadOnlyList<DashboardWidget> Widgets);
public sealed record InstitutionTrendPoint(string Label, int Count, bool IsPartial);
public sealed record PlatformDashboard(int? InstitutionsTotal, int? ActiveInstitutions, int? SuspendedInstitutions,
    int? AccountsTotal, IReadOnlyList<InstitutionTrendPoint> InstitutionsTrend, IReadOnlyList<AuditRecord> RecentActions);

public interface IDashboardPreferenceStore
{
    Task<DashboardPreference?> FindAsync(int accountId, CancellationToken cancellationToken);
    Task<DashboardPreference> SaveAsync(int accountId, DashboardPreference preference, CancellationToken cancellationToken);
}
