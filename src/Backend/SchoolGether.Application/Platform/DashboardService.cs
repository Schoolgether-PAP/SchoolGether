using SchoolGether.Application.Audit;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Institutions;

namespace SchoolGether.Application.Platform;

public sealed class DashboardService(PlatformAuthorization authorization, IPlatformInstitutionDirectory institutions,
    IPlatformAccountReader accounts, IAuditReader history, IAuditWriter audit, IDashboardPreferenceStore preferences,
    IAdministrativeUnitOfWork unitOfWork, TimeProvider clock)
{
    private static readonly DashboardWidget[] Defaults = [new("institutions-trend", "wide", true),
        new("institutions-status", "small", true), new("recent-actions", "full", true)];

    public async Task<PlatformDashboard> GetAsync(CancellationToken cancellationToken)
    {
        var actor = await authorization.GetContextAsync(cancellationToken);
        InstitutionStatistics? statistics = null;
        int? count = null;
        IReadOnlyList<InstitutionTrendPoint> trend = [];
        IReadOnlyList<AuditRecord> recent = [];
        if (actor.Permissions.Contains(PlatformCapabilities.InstitutionsRead))
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            statistics = await institutions.StatisticsAsync(month.AddMonths(-5), now, cancellationToken);
            count = await accounts.CountAllAsync(cancellationToken);
            trend = Enumerable.Range(0, 6).Select(i => month.AddMonths(i - 5)).Select(date => new InstitutionTrendPoint(
                date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
                statistics.Months.FirstOrDefault(x => x.Year == date.Year && x.Month == date.Month)?.Count ?? 0, date == month)).ToArray();
        }
        if (actor.Permissions.Contains(PlatformCapabilities.AuditRead))
            recent = (await history.ListAsync(1, 10, null, cancellationToken)).Items;
        await audit.AppendIndependentAsync(Event(actor, "platform.dashboard.read", "Consulta da visão geral"), cancellationToken);
        return new PlatformDashboard(statistics?.Total, statistics?.Active, statistics?.Suspended, count, trend, recent);
    }

    public async Task<DashboardPreference> GetPreferencesAsync(CancellationToken cancellationToken)
    {
        var actor = await authorization.GetContextAsync(cancellationToken);
        var value = await preferences.FindAsync(actor.Id, cancellationToken) ?? new DashboardPreference(1, 0, Defaults);
        await audit.AppendIndependentAsync(Event(actor, "platform.dashboard.preferences.read", "Consulta das preferências"), cancellationToken);
        return Filter(value, actor);
    }

    public async Task<DashboardPreference> SavePreferencesAsync(DashboardPreference preference, CancellationToken cancellationToken)
    {
        var actor = await authorization.GetContextAsync(cancellationToken);
        try
        {
            Validate(preference);
            var result = await unitOfWork.ExecuteAsync(async token =>
            {
                var saved = await preferences.SaveAsync(actor.Id, preference, token);
                audit.Append(Event(actor, "platform.dashboard.preferences.save", "Alteração das preferências"));
                return saved;
            }, cancellationToken);
            return Filter(result, actor);
        }
        catch (PlatformException exception)
        {
            await audit.AppendIndependentAsync(Event(actor, "platform.dashboard.preferences.save", exception.Code, "refused"), cancellationToken);
            throw;
        }
    }

    private static void Validate(DashboardPreference value)
    {
        if (value.SchemaVersion != 1 || value.RowVersion < 0 || value.Widgets is null || value.Widgets.Count > 3 ||
            value.Widgets.Any(x => x is null || !Defaults.Any(d => d.Id == x.Id) || x.Size is not ("small" or "wide" or "full")) ||
            value.Widgets.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != value.Widgets.Count)
            throw new PlatformException("InvalidDashboardPreference");
    }

    private static DashboardPreference Filter(DashboardPreference value, PlatformAdmin actor) => value with {
        Widgets = value.Widgets.Where(x => actor.Permissions.Contains(x.Id == "recent-actions" ?
            PlatformCapabilities.AuditRead : PlatformCapabilities.InstitutionsRead)).ToArray() };
    private static AuditEvent Event(PlatformAdmin actor, string action, string reason, string result = "success") =>
        new(new AuditActor(actor.Id, actor.Name), action, actor.Id.ToString(), result, reason);
}
