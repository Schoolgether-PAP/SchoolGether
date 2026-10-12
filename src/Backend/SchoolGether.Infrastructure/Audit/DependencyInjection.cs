using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Audit;

namespace SchoolGether.Infrastructure.Audit;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IAuditReader, AuditReader>();
        services.AddScoped<IAdministrativeUnitOfWork, AdministrativeUnitOfWork>();
        return services;
    }
}
