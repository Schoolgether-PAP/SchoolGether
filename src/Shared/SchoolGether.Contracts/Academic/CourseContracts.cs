using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SchoolGether.Contracts.Academic;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateCourseRequest(Guid ClientId, [Required, TrimmedLength(30)] string Code,
    [Required, TrimmedLength(200)] string Name, [TrimmedLength(1000)] string? Description);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateCourseRequest([Range(1, long.MaxValue)] long RowVersion,
    [Required, TrimmedLength(30)] string Code, [Required, TrimmedLength(200)] string Name,
    [TrimmedLength(1000)] string? Description);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CourseStateRequest([Range(1, long.MaxValue)] long RowVersion);
public sealed record CourseResponse(int Id, int InstitutionId, Guid ClientId, string Code, string Name, string? Description,
    bool IsActive, long RowVersion, DateTime CreatedAt, DateTime? UpdatedAt, bool Replay = false);
public sealed record CoursePageResponse(IReadOnlyList<CourseResponse> Items, int Total, int Page, int PageSize);

// A validação do contrato usa o mesmo limite normalizado aceito pelo servidor.
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class TrimmedLengthAttribute(int maximum) : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is string text && text.Trim().Length <= maximum;
}
