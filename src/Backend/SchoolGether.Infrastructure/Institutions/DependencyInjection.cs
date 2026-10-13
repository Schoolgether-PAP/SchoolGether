using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Institutions;

namespace SchoolGether.Infrastructure.Institutions;

public static class DependencyInjection
{
    public static IServiceCollection AddInstitutionsModule(this IServiceCollection services)
    {
        services.AddScoped<IInstitutionDirectory, InstitutionDirectory>();
        services.AddScoped<InstitutionService>();
        services.AddScoped<IPlatformInstitutionDirectory, PlatformInstitutionDirectory>();
        services.AddScoped<PlatformInstitutionService>();
        return services;
    }
}
