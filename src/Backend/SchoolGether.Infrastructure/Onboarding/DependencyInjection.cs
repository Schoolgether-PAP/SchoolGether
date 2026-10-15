using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Onboarding;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Institutions;
using SchoolGether.Application.Billing;
using SchoolGether.Infrastructure.Identity;
using SchoolGether.Infrastructure.Institutions;
using SchoolGether.Infrastructure.Billing;

namespace SchoolGether.Infrastructure.Onboarding;

public static class DependencyInjection
{
    public static IServiceCollection AddOnboardingModule(this IServiceCollection services)
    {
        services.AddScoped<OnboardingService>();
        services.AddScoped<IOnboardingUnitOfWork, OnboardingUnitOfWork>();
        services.AddScoped<IOnboardingAccountProvisioner, OnboardingAccountProvisioner>();
        services.AddScoped<IInstitutionRegistrationStore, InstitutionRegistrationStore>();
        services.AddScoped<ISubscriptionStore, SubscriptionStore>();
        return services;
    }
}
