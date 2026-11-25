namespace SchoolGether.Domain.Calendar;

public sealed class CalendarException(string code) : Exception(code) { public string Code { get; } = code; }
public sealed class SchoolEvent
{
    public int Id { get; private set; }
    public Guid ClientId { get; private set; } = Guid.NewGuid();
    public int InstitutionId { get; private set; }
    public int CreatedBy { get; private set; }
    public string Title { get; private set; } = ""; public string? Details { get; private set; }
    public string? Location { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public string Status { get; private set; } = "Pending"; public long RowVersion { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    private SchoolEvent() { }
    public static SchoolEvent Create(int institution, int actor, bool publish, string title, string? details, string? location, DateTimeOffset start, DateTimeOffset end, DateTime now, Guid? clientId = null)
    {
        if (clientId == Guid.Empty) throw new CalendarException("InvalidInput");
        var x = new SchoolEvent { InstitutionId = institution, CreatedBy = actor, Status = publish ? "Published" : "Pending", CreatedAt = now, ClientId = clientId ?? Guid.NewGuid() };
        x.Fields(title, details, location, start, end);
        return x;
    }
    public void Update(long version, string title, string? details, string? location, DateTimeOffset start, DateTimeOffset end, DateTime now)
    { Check(version); if (Status is not ("Pending" or "Published")) throw new CalendarException("StateConflict"); Fields(title, details, location, start, end); Touch(now); }
    public void Change(long version, string operation, DateTime now)
    { Check(version); Status = operation switch { "approve" when Status == "Pending" => "Published", "reject" when Status == "Pending" => "Rejected", "cancel" when Status is "Pending" or "Published" => "Cancelled", _ => throw new CalendarException("StateConflict") }; Touch(now); }
    private void Check(long version) { if (version != RowVersion) throw new CalendarException("VersionConflict"); }
    private void Touch(DateTime now) { RowVersion++; UpdatedAt = now; }
    private void Fields(string title, string? details, string? location, DateTimeOffset start, DateTimeOffset end)
    { if (string.IsNullOrWhiteSpace(title) || title.Length > 200 || details?.Length > 4000 || location?.Length > 200 || start == default || end <= start || end - start > TimeSpan.FromDays(31)) throw new CalendarException("InvalidInput"); Title = title.Trim(); Details = details?.Trim(); Location = location?.Trim(); StartUtc = start.UtcDateTime; EndUtc = end.UtcDateTime; }
}
