using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SchoolGether.Contracts.Platform;

public static class PlatformPermissions
{
    public const string InstitutionsRead = "institutions.read";
    public const string InstitutionsWrite = "institutions.write";
    public const string InstitutionsSuspend = "institutions.suspend";
    public const string AuditRead = "audit.read";
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] {
        InstitutionsRead, InstitutionsWrite, InstitutionsSuspend, AuditRead });
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record PlatformContextResponse(int AccountId, bool IsMaster, string[] Permissions, long RowVersion);
public sealed record PlatformAdminResponse(int Id, string Email, string Name, bool IsMaster, bool IsActive, string[] Permissions, long RowVersion);
public sealed class CreatePlatformAdminRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(4)] public string[] Permissions { get; set; } = [];
}
public sealed class UpdatePlatformAdminRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    [Required, MaxLength(4)] public string[] Permissions { get; set; } = [];
    [Range(1, long.MaxValue)] public long RowVersion { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
}
public sealed record AuditResponse(int Id, DateTime CreatedAt, int ActorId, string ActorName, int? InstitutionId,
    string Action, string Target, string Result, string Reason, JsonElement? Before, JsonElement? After);
