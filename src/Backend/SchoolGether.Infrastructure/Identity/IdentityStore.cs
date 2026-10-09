using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Identity;

internal sealed class IdentityStore(UserManager<ApplicationUser> users, SchoolGetherDbContext db,
    TimeProvider clock) : IIdentityStore
{
    public async Task<Account?> VerifyCredentialsAsync(string email, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(email);
        if (user is null || user.DeletedAt.HasValue || await users.IsLockedOutAsync(user)) return null;
        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return null;
        }
        await users.ResetAccessFailedCountAsync(user);
        return await ToAccountAsync(user, cancellationToken);
    }

    public async Task<Account?> FindForAuthenticationAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.DeletedAt == null, cancellationToken);
        if (user is null || (user.LockoutEnabled && user.LockoutEnd > clock.GetUtcNow())) return null;
        return await ToAccountAsync(user, cancellationToken);
    }

    private async Task<Account?> ToAccountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (user.IsPlatformAdmin)
            return new Account(user.Id, user.Email!, user.Name, AccountRole.PlatformAdmin, null, user.SecurityStamp!);
        // Contorno do filtro limitado à autenticação de uma conta identificada pelo servidor.
        var member = await db.Members.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(
            x => x.UserId == user.Id && x.DeletedAt == null, cancellationToken);
        if (member is null) return null;
        // A desativação da instituição também impede entrar e renovar a sessão.
        if (!await db.Institutions.AnyAsync(x => x.Id == member.InstitutionId, cancellationToken)) return null;
        return new Account(user.Id, user.Email!, member.Name, member.Role, member.InstitutionId, user.SecurityStamp!);
    }

    public async Task<Account> CreateAsync(CreateAccount account, int createdBy, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser { UserName = account.Email.Trim(), Email = account.Email.Trim(),
            Name = account.Name.Trim(), CreatedBy = createdBy, LockoutEnabled = true };
        IdentityResult result;
        try { result = await users.CreateAsync(user, account.Password); }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
        {
            throw new IdentityException("AccountExists");
        }
        if (!result.Succeeded)
            throw new IdentityException(result.Errors.Any(x => x.Code.StartsWith("Duplicate", StringComparison.Ordinal))
                ? "AccountExists" : "InvalidAccount");
        db.Members.Add(new InstitutionMember(user.Id, account.InstitutionId!.Value, account.Name, account.Role, createdBy));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Account(user.Id, user.Email!, user.Name, account.Role, account.InstitutionId, user.SecurityStamp!);
    }

    public async Task<MemberPage> ListMembersAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Members.AsNoTracking().Join(db.Users.Where(x => x.DeletedAt == null),
            member => member.UserId, user => user.Id,
            (member, user) => new { Member = member, User = user });
        var total = await query.CountAsync(cancellationToken);
        var members = await query.OrderBy(x => x.User.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new Account(x.User.Id, x.User.Email!, x.Member.Name, x.Member.Role, x.Member.InstitutionId, string.Empty))
            .ToListAsync(cancellationToken);
        return new MemberPage(members, total, page, pageSize);
    }
}
