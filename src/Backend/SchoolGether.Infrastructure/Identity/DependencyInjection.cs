using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        }).AddEntityFrameworkStores<SchoolGetherDbContext>();
        services.AddSingleton<TokenSigningKey>();
        services.AddScoped<IAccessTokenIssuer, AccessTokenIssuer>();
        services.AddScoped<IIdentityStore, IdentityStore>();
        services.AddScoped<ISessionStore, SessionStore>();
        services.AddScoped<IdentityService>();
        services.AddScoped<IPlatformAdminBootstrap, PlatformAdminBootstrap>();
        return services;
    }
}
