using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Billing;

namespace SchoolGether.Infrastructure.Billing;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingModule(this IServiceCollection services)
    {
        services.AddSingleton<PlanCatalog>();
        services.AddScoped<IInstitutionAccountCapacity, InstitutionAccountCapacity>();
        return services;
    }
}
