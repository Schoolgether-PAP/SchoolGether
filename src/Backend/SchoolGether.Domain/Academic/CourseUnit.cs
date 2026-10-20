using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SchoolGether.Domain.Academic;

public sealed class CourseUnit
{
    private CourseUnit() { }
    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public int CourseId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Syllabus { get; private set; }
    public int? StudyYear { get; private set; }
    public bool IsActive { get; private set; } = true;
    public long RowVersion { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string CreationFingerprint { get; private set; } = string.Empty;

    public static CourseUnit Create(int institutionId, int courseId, int actorId, Guid clientId, string code, string name, string? syllabus, DateTime now, int? studyYear = null)
    {
        if (institutionId <= 0 || courseId <= 0 || actorId <= 0 || clientId == Guid.Empty) throw new CourseRuleException("InvalidCourseUnitInput");
        RequireUtc(now);
        RequireStudyYear(studyYear);
        var fields = Normalize(code, name, syllabus);
        return new CourseUnit
        {
            InstitutionId = institutionId,
            CourseId = courseId,
            CreatedBy = actorId,
            ClientId = clientId,
            Code = fields.Code,
            Name = fields.Name,
            Syllabus = fields.Syllabus,
            StudyYear = studyYear,
            CreatedAt = now,
            CreationFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(studyYear is null
                ? JsonSerializer.Serialize(new[] { fields.Code, fields.Name, fields.Syllabus })
                : JsonSerializer.Serialize(new object?[] { "v2", fields.Code, fields.Name, fields.Syllabus, studyYear }))))
        };
    }
    public bool IsReplayOf(CourseUnit initial) => InstitutionId == initial.InstitutionId && CourseId == initial.CourseId && ClientId == initial.ClientId &&
        CreatedBy == initial.CreatedBy && CreationFingerprint == initial.CreationFingerprint;
    public void Update(long version, string code, string name, string? syllabus, DateTime now, int? studyYear = null)
    {
        RequireVersion(version, now);
        if (!IsActive) throw new CourseRuleException("CourseUnitArchived");
        RequireStudyYear(studyYear);
        var fields = Normalize(code, name, syllabus);
        Code = fields.Code; Name = fields.Name; Syllabus = fields.Syllabus; StudyYear = studyYear ?? StudyYear; Advance(now);
    }
    public void Archive(long version, DateTime now)
    {
        RequireVersion(version, now);
        if (!IsActive) throw new CourseRuleException("CourseUnitArchived");
        IsActive = false; DeletedAt = now; Advance(now);
    }
    public void Restore(long version, DateTime now)
    {
        RequireVersion(version, now);
        if (IsActive) throw new CourseRuleException("CourseUnitAlreadyActive");
        IsActive = true; DeletedAt = null; Advance(now);
    }
    private void RequireVersion(long version, DateTime now)
    {
        RequireUtc(now);
        if (version < 1) throw new CourseRuleException("InvalidCourseUnitInput");
        if (version != RowVersion) throw new CourseRuleException("VersionConflict");
    }
    private static void RequireUtc(DateTime now)
    { if (now.Kind != DateTimeKind.Utc) throw new CourseRuleException("InvalidCourseUnitInput"); }
    private static void RequireStudyYear(int? year)
    { if (year is < 1 or > 20) throw new CourseRuleException("InvalidCourseUnitInput"); }
    private void Advance(DateTime now) { RowVersion = checked(RowVersion + 1); UpdatedAt = now; }
    private static (string Code, string Name, string? Syllabus) Normalize(string code, string name, string? syllabus)
    {
        code = code?.Trim() ?? string.Empty; name = name?.Trim() ?? string.Empty; syllabus = syllabus?.Trim();
        if (code.Length is < 1 or > 30 || !Regex.IsMatch(code, "\\A[A-Za-z0-9][A-Za-z0-9._-]*\\z", RegexOptions.CultureInvariant) ||
            name.Length is < 1 or > 200 || syllabus?.Length > 5000) throw new CourseRuleException("InvalidCourseUnitInput");
        return (code.ToUpperInvariant(), name, string.IsNullOrWhiteSpace(syllabus) ? null : syllabus);
    }
}
