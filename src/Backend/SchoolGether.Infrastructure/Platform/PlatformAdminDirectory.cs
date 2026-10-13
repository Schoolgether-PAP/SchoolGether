using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SchoolGether.Application.Platform;
using SchoolGether.Infrastructure.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Platform;

internal sealed class PlatformAdminDirectory(SchoolGetherDbContext db, UserManager<ApplicationUser> users,
    TimeProvider clock) : IPlatformAdminDirectory
{
    public async Task<PlatformAdmin?> FindAsync(int id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsPlatformAdmin, cancellationToken);
        return user is null ? null : ToModel(user);
    }

    public async Task<PlatformAdminPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking().Where(x => x.IsPlatformAdmin && x.DeletedAt == null);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PlatformAdminPage(items.Select(ToModel).ToArray(), total, page, pageSize);
    }

    public async Task<PlatformAdmin> CreateAsync(CreatePlatformAdmin request, int createdBy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 ||
            string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 150 || request.Password.Length is < 12 or > 128)
            throw new PlatformException("InvalidAccount");
        var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim(),
            IsPlatformAdmin = true, CreatedBy = createdBy, LockoutEnabled = true };
        user.SetPlatformAccess(request.Name, true, request.Permissions);
        IdentityResult result;
        try { result = await users.CreateAsync(user, request.Password); }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
        { throw new PlatformException("AccountExists"); }
        if (!result.Succeeded) throw new PlatformException(result.Errors.Any(x => x.Code.StartsWith("Duplicate", StringComparison.Ordinal))
            ? "AccountExists" : "InvalidAccount");
        return ToModel(user);
    }

    public async Task<PlatformAdmin> UpdateAsync(int id, UpdatePlatformAdmin request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.IsPlatformAdmin && x.DeletedAt == null, cancellationToken)
            ?? throw new PlatformException("PlatformAdminNotFound");
        if (user.RowVersion != request.RowVersion) throw new PlatformException("VersionConflict");
        user.SetPlatformAccess(request.Name, request.IsActive, request.Permissions);
        user.RowVersion++; user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        return ToModel(user);
    }

    public async Task<PlatformAdmin> PromoteMasterAsync(int id, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(x => x.MasterSlot != null, cancellationToken)) throw new PlatformException("MasterExists");
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.IsPlatformAdmin && x.IsActive && x.DeletedAt == null, cancellationToken)
            ?? throw new PlatformException("InvalidMasterAccount");
        if (user.LockoutEnabled && user.LockoutEnd > clock.GetUtcNow()) throw new PlatformException("InvalidMasterAccount");
        user.PromoteMaster();
        return ToModel(user);
    }

    private PlatformAdmin ToModel(ApplicationUser user) => new(user.Id, user.Email!, user.Name, user.IsMaster,
        user.IsActive && user.DeletedAt == null && !(user.LockoutEnabled && user.LockoutEnd > clock.GetUtcNow()),
        user.GetPermissions(), user.RowVersion);
}
