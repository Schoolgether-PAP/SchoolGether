using SchoolGether.Domain.Calendar;
namespace SchoolGether.Domain.Learning;

public sealed class LessonPlan
{
    public int Id { get; private set; }
    public Guid ClientId { get; private set; }
    public int InstitutionId { get; private set; }
    public int TeacherId { get; private set; }
    public int CourseId { get; private set; }
    public int ClassId { get; private set; }
    public int UnitId { get; private set; }
    public string AcademicYear { get; private set; } = "";
    public string Title { get; private set; } = "";
    public DateOnly Date { get; private set; }
    public string? Objectives { get; private set; }
    public string? Content { get; private set; }
    public string? Resources { get; private set; }
    public string CreateHash { get; private set; } = "";
    public bool IsArchived { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    private LessonPlan() { }
    public static LessonPlan Create(int tenant, int teacher, Guid key, int course, int group, int unit, string year, string title, DateOnly date, string? objectives, string? content, string? resources, string hash, DateTime now)
    {
        if (key == Guid.Empty || course <= 0 || group <= 0 || unit <= 0) throw new CalendarException("InvalidInput");
        var p = new LessonPlan { InstitutionId = tenant, TeacherId = teacher, ClientId = key, CourseId = course, ClassId = group, UnitId = unit, AcademicYear = year, CreateHash = hash, CreatedAt = now };
        p.Fields(title, date, objectives, content, resources); return p;
    }
    public void Update(long version, string title, DateOnly date, string? objectives, string? content, string? resources, DateTime now)
    { Check(version); if (IsArchived) throw new CalendarException("StateConflict"); Fields(title, date, objectives, content, resources); Touch(now); }
    public void Archive(long version, DateTime now) { Check(version); if (IsArchived) throw new CalendarException("StateConflict"); IsArchived = true; Touch(now); }
    private void Check(long version) { if (version != RowVersion) throw new CalendarException("VersionConflict"); }
    private void Touch(DateTime now) { RowVersion++; UpdatedAt = now; }
    private void Fields(string title, DateOnly date, string? objectives, string? content, string? resources)
    { if (string.IsNullOrWhiteSpace(title) || title.Length > 200 || date == default || objectives?.Length > 4000 || content?.Length > 8000 || resources?.Length > 4000) throw new CalendarException("InvalidInput"); Title = title.Trim(); Date = date; Objectives = objectives?.Trim(); Content = content?.Trim(); Resources = resources?.Trim(); }
}
