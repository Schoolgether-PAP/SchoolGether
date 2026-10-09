using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Identity;

namespace SchoolGether.Infrastructure.Identity;

internal sealed class PlatformAdminBootstrap(UserManager<ApplicationUser> users) : IPlatformAdminBootstrap
{
    public async Task<bool> CreateAsync(string email, string password, CancellationToken cancellationToken)
    {
        if (await users.Users.AnyAsync(x => x.IsPlatformAdmin, cancellationToken)) return false;
        if (password.Length > 128) throw new BootstrapValidationException(["PasswordTooLong"]);
        var user = new ApplicationUser { UserName = email.Trim(), Email = email.Trim(),
            Name = email.Trim(), IsPlatformAdmin = true, LockoutEnabled = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new BootstrapValidationException(result.Errors.Select(error => error.Code).Distinct().ToArray());
        return true;
    }
}
