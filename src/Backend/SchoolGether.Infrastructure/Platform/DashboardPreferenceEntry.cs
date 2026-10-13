namespace SchoolGether.Infrastructure.Platform;

internal sealed class DashboardPreferenceEntry
{
    public int AccountId { get; set; }
    public int SchemaVersion { get; set; }
    public string WidgetsJson { get; set; } = "[]";
    public long RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
