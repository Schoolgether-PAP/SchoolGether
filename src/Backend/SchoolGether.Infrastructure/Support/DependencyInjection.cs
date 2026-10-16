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
        services.AddSingleton<LocalFileStorage>();
        services.AddScoped<OrphanFileCleanup>();

        services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<SupportOptions>().Enabled
            ? sp.GetRequiredService<LocalFileStorage>() : new DisabledFileStorage());
        return services;
    }
    private sealed class DisabledFileStorage : IFileStorage
    {
        public Task<PreparedAttachment> PrepareAsync(IncomingAttachment file, CancellationToken ct) => throw new SupportException("SupportDisabled");
        public Task CommitAsync(PreparedAttachment file, CancellationToken ct) => throw new SupportException("SupportDisabled");
        public Task DeleteAsync(string key, CancellationToken ct) => throw new SupportException("SupportDisabled");
        public Task ReleaseAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => throw new SupportException("SupportDisabled");
    }
    private sealed class NullSupportNotifier : ISupportNotifier
    { public Task ChangedAsync(int ticketId, bool internalOnly, CancellationToken ct) => Task.CompletedTask; }
}
