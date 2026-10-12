namespace SchoolGether.Application.Platform;

public static class PlatformCapabilities
{
    public const string InstitutionsRead = "institutions.read";
    public const string InstitutionsWrite = "institutions.write";
    public const string InstitutionsSuspend = "institutions.suspend";
    public const string AuditRead = "audit.read";
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] {
        InstitutionsRead, InstitutionsWrite, InstitutionsSuspend, AuditRead });

    public static string[] Normalize(IEnumerable<string>? permissions)
    {
        var values = (permissions ?? []).Select(x => x?.Trim().ToLowerInvariant() ?? string.Empty)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (values.Any(x => !All.Contains(x))) throw new PlatformException("InvalidPermissions");
        return values;
    }
}

public sealed class PlatformException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed record PlatformAdmin(int Id, string Email, string Name, bool IsMaster, bool IsActive,
    string[] Permissions, long RowVersion);
public sealed record PlatformAdminPage(IReadOnlyList<PlatformAdmin> Items, int Total, int Page, int PageSize);
public sealed record CreatePlatformAdmin(string Email, string Password, string Name, string[] Permissions);
public sealed record UpdatePlatformAdmin(string Name, bool IsActive, string[] Permissions, long RowVersion, string Reason);

public interface IPlatformAdminDirectory
{
    Task<PlatformAdmin?> FindAsync(int id, CancellationToken cancellationToken);
    Task<PlatformAdminPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PlatformAdmin> CreateAsync(CreatePlatformAdmin request, int createdBy, CancellationToken cancellationToken);
    Task<PlatformAdmin> UpdateAsync(int id, UpdatePlatformAdmin request, CancellationToken cancellationToken);
    Task<PlatformAdmin> PromoteMasterAsync(int id, CancellationToken cancellationToken);
}
