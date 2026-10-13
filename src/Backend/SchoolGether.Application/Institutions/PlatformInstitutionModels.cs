using SchoolGether.Application.Identity;

namespace SchoolGether.Application.Institutions;

public sealed record PlatformInstitution(int Id, string Name, bool IsSuspended, string? SuspensionReason,
    DateTime CreatedAt, int AccountsCount, long RowVersion);
public sealed record PlatformInstitutionDetail(PlatformInstitution Institution, IReadOnlyList<Account> Administrators);
public sealed record PlatformInstitutionPage(IReadOnlyList<PlatformInstitution> Items, int Total, int Page, int PageSize);
public sealed record CreatePlatformInstitution(string Name, string Reason);
public sealed record UpdatePlatformInstitution(string Name, long RowVersion, string Reason);
public sealed record SetInstitutionSuspension(bool IsSuspended, long RowVersion, string Reason);
public sealed record CreateInstitutionAdministrator(string Email, string Password, string Name, string Reason);
public sealed record InstitutionMonthlyCount(int Year, int Month, int Count);
public sealed record InstitutionStatistics(int Total, int Active, int Suspended, IReadOnlyList<InstitutionMonthlyCount> Months);

// Operações globais explícitas; nunca altera o filtro escolar dos outros módulos.
public interface IPlatformInstitutionDirectory
{
    Task<PlatformInstitutionPage> ListAsync(int page, int pageSize, string? search, bool? isSuspended, CancellationToken cancellationToken);
    Task<PlatformInstitution?> FindAsync(int id, CancellationToken cancellationToken);
    Task<PlatformInstitution> CreateAsync(string name, int createdBy, CancellationToken cancellationToken);
    Task<PlatformInstitution> RenameAsync(int id, string name, long rowVersion, CancellationToken cancellationToken);
    Task<PlatformInstitution> SetSuspensionAsync(int id, bool isSuspended, string reason, long rowVersion, CancellationToken cancellationToken);
    Task<InstitutionStatistics> StatisticsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}
