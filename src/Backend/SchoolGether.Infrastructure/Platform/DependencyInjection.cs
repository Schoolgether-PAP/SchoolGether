using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Platform;

namespace SchoolGether.Infrastructure.Platform;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformModule(this IServiceCollection services)
    {
        services.AddScoped<IPlatformAdminDirectory, PlatformAdminDirectory>();
        services.AddScoped<PlatformAuthorization>();
        services.AddScoped<PlatformAdminService>();
        services.AddScoped<MasterPromotionService>();
        return services;
    }
}
