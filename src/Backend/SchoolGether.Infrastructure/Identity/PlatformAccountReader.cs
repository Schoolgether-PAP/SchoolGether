using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Identity;

internal sealed class PlatformAccountReader(SchoolGetherDbContext db) : IPlatformAccountReader
{
    // Contorno limitado a agregados administrativos e responsáveis; nunca modifica o filtro scoped.
    private IQueryable<InstitutionMember> Members => db.Members.IgnoreQueryFilters().AsNoTracking().Where(x => x.DeletedAt == null);
    public async Task<IReadOnlyDictionary<int, int>> CountByInstitutionsAsync(IReadOnlyList<int> institutionIds, CancellationToken cancellationToken)
    {
        if (institutionIds.Count == 0) return new Dictionary<int, int>();
        return await Members.Where(x => institutionIds.Contains(x.InstitutionId))
            .Join(db.Users.Where(x => x.DeletedAt == null), member => member.UserId, user => user.Id, (member, user) => member)
            .GroupBy(x => x.InstitutionId).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
    }
    public Task<int> CountAllAsync(CancellationToken cancellationToken) =>
        db.Users.CountAsync(x => x.DeletedAt == null, cancellationToken);
    public async Task<IReadOnlyList<Account>> ListAdministratorsAsync(int institutionId, CancellationToken cancellationToken) =>
        await Members.Where(x => x.InstitutionId == institutionId && x.Role == AccountRole.InstitutionAdmin)
            .Join(db.Users.Where(x => x.DeletedAt == null), member => member.UserId, user => user.Id,
                (member, user) => new { Member = member, User = user })
            .OrderBy(x => x.User.Id)
            .Select(x => new Account(x.User.Id, x.User.Email!, x.Member.Name, x.Member.Role, x.Member.InstitutionId, string.Empty))
            .ToListAsync(cancellationToken);
}
