using SchoolGether.Infrastructure.Support;
using SchoolGether.Application.Support;

namespace SchoolGether.Api.Support;

public static class SupportRegistration
{
    public static IServiceCollection AddApiSupport(this IServiceCollection services)
    {
        services.AddSupportModule();
        services.AddSingleton(sp => new SupportOptions { Enabled = sp.GetRequiredService<IConfiguration>().GetValue<bool>("Support:Enabled") });
        services.AddExceptionHandler<SupportExceptionHandler>();
        return services;
    }
}
