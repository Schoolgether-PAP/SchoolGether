using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Identity;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Infrastructure.Identity;

internal sealed class InstitutionStudentDirectory(SchoolGetherDbContext db,TimeProvider clock,ICurrentAccount current):IInstitutionStudentDirectory
{
    // Projeção mínima: nunca materializa contas, credenciais ou security stamps.
    private sealed class Candidate { public int UserId { get; init; } public string Name { get; init; } = string.Empty; public string Email { get; init; } = string.Empty; public bool Active { get; init; } public DateTime? UserDeleted { get; init; } public DateTime? MemberDeleted { get; init; } public bool LockoutEnabled { get; init; } public DateTimeOffset? LockoutEnd { get; init; } }
    private IQueryable<Candidate> Query(int tenant)
    {
        if(current.InstitutionId!=tenant||current.UserId is not >0||current.Role!="InstitutionAdmin")throw new InvalidOperationException("DirectoryAccessDenied");
        return from member in db.Members.IgnoreQueryFilters().AsNoTracking()
        join user in db.Users.AsNoTracking() on member.UserId equals user.Id
        where member.InstitutionId==tenant&&member.Role==AccountRole.Student
        select new Candidate { UserId=user.Id, Name=member.Name, Email=user.Email??string.Empty, Active=user.IsActive, UserDeleted=user.DeletedAt, MemberDeleted=member.DeletedAt, LockoutEnabled=user.LockoutEnabled, LockoutEnd=user.LockoutEnd };
    }
    private StudentDirectoryItem Describe(Candidate x)=>new(x.UserId,x.Name,x.Email,x.Active&&x.UserDeleted is null&&x.MemberDeleted is null&&(!x.LockoutEnabled||x.LockoutEnd is null||x.LockoutEnd<=clock.GetUtcNow()));
    private static bool Matches(StudentDirectoryItem x,string? search)=>string.IsNullOrWhiteSpace(search)||x.Name.Contains(search.Trim(),StringComparison.OrdinalIgnoreCase)||x.Email.Contains(search.Trim(),StringComparison.OrdinalIgnoreCase);
    public async Task<StudentDirectoryPage> ListAsync(int institutionId,int page,int pageSize,string? search,IReadOnlyCollection<int> excludedIds,CancellationToken ct)
    {
        // LockoutEnd não se compara em SQL SQLite. Disponibilidade/pesquisa/exclusões antecedem totais e paginação em todos os providers.
        var rows=(await Query(institutionId).Where(x=>!excludedIds.Contains(x.UserId)).ToListAsync(ct)).Select(Describe).Where(x=>x.Available&&Matches(x,search)).OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.UserId).ToArray();
        return new(rows.Skip((page-1)*pageSize).Take(pageSize).ToArray(),rows.Length,page,pageSize);
    }
    public async Task<IReadOnlyList<int>> SearchIdsAsync(int institutionId,string search,CancellationToken ct)=>(await Query(institutionId).ToListAsync(ct)).Select(Describe).Where(x=>Matches(x,search)).Select(x=>x.UserId).ToArray();
    public async Task<IReadOnlyList<StudentDirectoryItem>> FindManyAsync(int institutionId,IReadOnlyCollection<int> ids,CancellationToken ct)=>(await Query(institutionId).Where(x=>ids.Contains(x.UserId)).ToListAsync(ct)).Select(Describe).ToArray();
}
