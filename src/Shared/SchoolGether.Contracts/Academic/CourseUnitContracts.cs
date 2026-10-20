using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SchoolGether.Contracts.Academic;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCourseUnitRequest(Guid ClientId, [Required, TrimmedLength(30)] string Code,
    [Required, TrimmedLength(200)] string Name, [TrimmedLength(5000)] string? Syllabus);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateCourseUnitRequest([Range(1, long.MaxValue)] long RowVersion,
    [Required, TrimmedLength(30)] string Code, [Required, TrimmedLength(200)] string Name,
    [TrimmedLength(5000)] string? Syllabus);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CourseUnitStateRequest([Range(1, long.MaxValue)] long RowVersion);
public sealed record CourseUnitResponse(int Id, int InstitutionId, int CourseId, Guid ClientId, string Code, string Name, string? Syllabus,
    bool IsActive, long RowVersion, DateTime CreatedAt, DateTime? UpdatedAt, bool Replay = false);
public sealed record CourseUnitPageResponse(IReadOnlyList<CourseUnitResponse> Items, int Total, int Page, int PageSize);
