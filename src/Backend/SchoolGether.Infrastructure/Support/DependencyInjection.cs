using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Support;

namespace SchoolGether.Infrastructure.Support;

public static class DependencyInjection
{
    public static IServiceCollection AddSupportModule(this IServiceCollection services)
    {
        services.AddScoped<SupportAuthorization>(); services.AddScoped<SupportService>(); services.AddScoped<SupportQueries>();
        services.AddScoped<ISupportStore, SupportStore>(); services.AddScoped<ISupportUnitOfWork, SupportUnitOfWork>();
        services.AddSingleton<ISupportNotifier, NullSupportNotifier>();
        return services;
    }
    private sealed class NullSupportNotifier : ISupportNotifier
    { public Task ChangedAsync(int ticketId, bool internalOnly, CancellationToken ct) => Task.CompletedTask; }
}
