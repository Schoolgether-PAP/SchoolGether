using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SchoolGether.Domain.Academic;

public sealed class CourseRuleException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class Course
{
    private Course() { }
    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public long RowVersion { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string CreationFingerprint { get; private set; } = string.Empty;

    public static Course Create(int institutionId, int actorId, Guid clientId, string code, string name, string? description, DateTime now)
    {
        if (institutionId <= 0 || actorId <= 0 || clientId == Guid.Empty) throw new CourseRuleException("InvalidCourseInput");
        RequireUtc(now);
        var fields = Normalize(code, name, description);
        return new Course { InstitutionId = institutionId, CreatedBy = actorId, ClientId = clientId,
            Code = fields.Code, Name = fields.Name, Description = fields.Description, CreatedAt = now,
            CreationFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { fields.Code, fields.Name, fields.Description })))) };
    }
    public bool IsReplayOf(Course initial) => InstitutionId == initial.InstitutionId && ClientId == initial.ClientId &&
        CreatedBy == initial.CreatedBy && CreationFingerprint == initial.CreationFingerprint;
    public void Update(long version, string code, string name, string? description, DateTime now)
    {
        RequireVersion(version, now);
        if (!IsActive) throw new CourseRuleException("CourseArchived");
        var fields = Normalize(code, name, description);
        Code = fields.Code; Name = fields.Name; Description = fields.Description; Advance(now);
    }
    public void TouchUnits(long version, DateTime now)
    {
        RequireVersion(version, now);
        if (!IsActive) throw new CourseRuleException("CourseArchived");
        Advance(now);
    }
    public void TouchClasses(long version, DateTime now) => TouchUnits(version, now);
    public void Archive(long version, DateTime now)
    {
        RequireVersion(version, now);
        if (!IsActive) throw new CourseRuleException("CourseArchived");
        IsActive = false; DeletedAt = now; Advance(now);
    }
    public void Restore(long version, DateTime now)
    {
        RequireVersion(version, now);
        if (IsActive) throw new CourseRuleException("CourseAlreadyActive");
        IsActive = true; DeletedAt = null; Advance(now);
    }
    private void RequireVersion(long version, DateTime now)
    {
        RequireUtc(now);
        if (version < 1) throw new CourseRuleException("InvalidCourseInput");
        if (version != RowVersion) throw new CourseRuleException("VersionConflict");
    }
    private static void RequireUtc(DateTime now)
    { if (now.Kind != DateTimeKind.Utc) throw new CourseRuleException("InvalidCourseInput"); }
    private void Advance(DateTime now) { RowVersion = checked(RowVersion + 1); UpdatedAt = now; }
    private static (string Code, string Name, string? Description) Normalize(string code, string name, string? description)
    {
        code = code?.Trim() ?? string.Empty; name = name?.Trim() ?? string.Empty; description = description?.Trim();
        if (code.Length is < 1 or > 30 || !Regex.IsMatch(code, "\\A[A-Za-z0-9][A-Za-z0-9._-]*\\z", RegexOptions.CultureInvariant) ||
            name.Length is < 1 or > 200 || description?.Length > 1000) throw new CourseRuleException("InvalidCourseInput");
        return (code.ToUpperInvariant(), name, string.IsNullOrWhiteSpace(description) ? null : description);
    }
}
