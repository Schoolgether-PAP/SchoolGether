namespace SchoolGether.Application.Identity;
public sealed record TeacherDirectoryItem(int UserId,string Name,string Email,bool Available);
public sealed record TeacherDirectoryPage(IReadOnlyList<TeacherDirectoryItem> Items,int Total,int Page,int PageSize);
public interface IInstitutionTeacherDirectory
{
    Task<TeacherDirectoryPage> ListAsync(int institutionId,int page,int pageSize,string? search,IReadOnlyCollection<int> excludedIds,CancellationToken ct);
    Task<IReadOnlyList<int>> SearchIdsAsync(int institutionId,string search,CancellationToken ct);
    Task<IReadOnlyList<TeacherDirectoryItem>> FindManyAsync(int institutionId,IReadOnlyCollection<int> ids,CancellationToken ct);
}
