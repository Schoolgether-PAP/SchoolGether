namespace SchoolGether.Application.Identity;
public sealed record StudentDirectoryItem(int UserId,string Name,string Email,bool Available);
public sealed record StudentDirectoryPage(IReadOnlyList<StudentDirectoryItem> Items,int Total,int Page,int PageSize);
public interface IInstitutionStudentDirectory
{
    Task<StudentDirectoryPage> ListAsync(int institutionId,int page,int pageSize,string? search,IReadOnlyCollection<int> excludedIds,CancellationToken ct);
    Task<IReadOnlyList<int>> SearchIdsAsync(int institutionId,string search,CancellationToken ct);
    Task<IReadOnlyList<StudentDirectoryItem>> FindManyAsync(int institutionId,IReadOnlyCollection<int> ids,CancellationToken ct);
}
