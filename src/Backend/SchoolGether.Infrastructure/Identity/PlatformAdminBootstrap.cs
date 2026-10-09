using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Identity;

namespace SchoolGether.Infrastructure.Identity;

internal sealed class PlatformAdminBootstrap(UserManager<ApplicationUser> users) : IPlatformAdminBootstrap
{
    public async Task<bool> CreateAsync(string email, string password, CancellationToken cancellationToken)
    {
        if (await users.Users.AnyAsync(x => x.IsPlatformAdmin, cancellationToken)) return false;
        var user = new ApplicationUser { UserName = email.Trim(), Email = email.Trim(),
            Name = email.Trim(), IsPlatformAdmin = true, LockoutEnabled = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) throw new IdentityException("InvalidAccount");
        return true;
    }
}
