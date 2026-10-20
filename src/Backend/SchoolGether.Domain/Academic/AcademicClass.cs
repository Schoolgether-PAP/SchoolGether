using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SchoolGether.Domain.Institutions;

namespace SchoolGether.Domain.Academic;

public sealed class AcademicClass
{
    private AcademicClass() { }
    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public int CourseId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int StudyYear { get; private set; }
    public string AcademicYear { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public long RowVersion { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string CreationFingerprint { get; private set; } = string.Empty;

    public static AcademicClass Create(int institutionId, int courseId, int actorId, Guid clientId, string code, string name,
        int studyYear, string academicYear, IReadOnlyList<int> unitIds, DateTime now)
    {
        if (institutionId <= 0 || courseId <= 0 || actorId <= 0 || clientId == Guid.Empty || studyYear is < 1 or > 20 || !InstitutionProfile.IsValidAcademicYear(academicYear)) throw Invalid();
        RequireUtc(now); var fields = Normalize(code, name); var ids = ValidateUnitIds(unitIds);
        return new() { InstitutionId = institutionId, CourseId = courseId, CreatedBy = actorId, ClientId = clientId,
            Code = fields.Code, Name = fields.Name, StudyYear = studyYear, AcademicYear = academicYear, CreatedAt = now,
            CreationFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new object[] { fields.Code, fields.Name, studyYear, academicYear, ids })))) };
    }
    public bool IsReplayOf(AcademicClass initial) => InstitutionId == initial.InstitutionId && CourseId == initial.CourseId && ClientId == initial.ClientId && CreatedBy == initial.CreatedBy && CreationFingerprint == initial.CreationFingerprint;
    public static int[] ValidateUnitIds(IReadOnlyList<int>? ids)
    {
        if (ids is null || ids.Count is < 1 or > 100 || ids.Any(x => x <= 0) || ids.Distinct().Count() != ids.Count) throw Invalid();
        return ids.Order().ToArray();
    }
    public void Update(long version, string code, string name, DateTime now)
    {
        RequireVersion(version, now); if (!IsActive) throw new CourseRuleException("ClassArchived");
        var fields = Normalize(code, name); Code = fields.Code; Name = fields.Name; Advance(now);
    }
    public void Archive(long version, DateTime now)
    { RequireVersion(version, now); if (!IsActive) throw new CourseRuleException("ClassArchived"); IsActive = false; DeletedAt = now; Advance(now); }
    public void Restore(long version, DateTime now)
    { RequireVersion(version, now); if (IsActive) throw new CourseRuleException("ClassAlreadyActive"); IsActive = true; DeletedAt = null; Advance(now); }
    private void RequireVersion(long version, DateTime now)
    { RequireUtc(now); if (version < 1) throw Invalid(); if (version != RowVersion) throw new CourseRuleException("VersionConflict"); }
    private static void RequireUtc(DateTime now) { if (now.Kind != DateTimeKind.Utc) throw Invalid(); }
    private void Advance(DateTime now) { RowVersion = checked(RowVersion + 1); UpdatedAt = now; }
    private static CourseRuleException Invalid() => new("InvalidClassInput");
    private static (string Code, string Name) Normalize(string code, string name)
    {
        code = code?.Trim() ?? string.Empty; name = name?.Trim() ?? string.Empty;
        if (code.Length is < 1 or > 30 || !Regex.IsMatch(code, "\\A[A-Za-z0-9][A-Za-z0-9._-]*\\z", RegexOptions.CultureInvariant) || name.Length is < 1 or > 200) throw Invalid();
        return (code.ToUpperInvariant(), name);
    }
}

public sealed class AcademicClassUnit
{
    private AcademicClassUnit() { }
    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public int CourseId { get; private set; }
    public int ClassId { get; private set; }
    public int UnitId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public long RowVersion { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public AcademicClass Class { get; private set; } = null!;
    public CourseUnit Unit { get; private set; } = null!;
    public static AcademicClassUnit Create(AcademicClass value, CourseUnit unit, int actorId, DateTime now)
    {
        if (actorId <= 0 || now.Kind != DateTimeKind.Utc || value.InstitutionId != unit.InstitutionId || value.CourseId != unit.CourseId || value.StudyYear != unit.StudyYear || !unit.IsActive) throw new CourseRuleException("InvalidClassSelection");
        return new() { InstitutionId = value.InstitutionId, CourseId = value.CourseId, ClassId = value.Id, UnitId = unit.Id, Class = value, Unit = unit, CreatedBy = actorId, CreatedAt = now };
    }
    public void SetSelected(bool selected, DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc) throw new CourseRuleException("InvalidClassInput");
        if (selected == IsActive) return;
        if (selected && !Unit.IsActive) throw new CourseRuleException("ClassUnitArchived");
        IsActive = selected; DeletedAt = selected ? null : now; UpdatedAt = now; RowVersion = checked(RowVersion + 1);
    }
}
