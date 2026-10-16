using SchoolGether.Infrastructure.Support;
using SchoolGether.Application.Support;

namespace SchoolGether.Api.Support;

public static class SupportRegistration
{
    public static IServiceCollection AddApiSupport(this IServiceCollection services)
    {
        services.AddSupportModule();
        services.AddHostedService<OrphanFileCleanupWorker>();
        services.AddSingleton(sp =>
        {
            var options = new SupportOptions(); sp.GetRequiredService<IConfiguration>().GetSection("Support").Bind(options);
            if (options.Enabled)
            {
                var environment = sp.GetRequiredService<IWebHostEnvironment>();
                var publicRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot")));
                if (!Path.IsPathFullyQualified(options.FilesRoot) || options.InstitutionStorageLimitBytes <= 0 ||
                    Path.GetFullPath(options.FilesRoot).StartsWith(publicRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.FilesRoot)), publicRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Support:FilesRoot exige um diretório privado absoluto fora de wwwroot e quota positiva.");
            }
            return options;
        });
        services.AddExceptionHandler<SupportExceptionHandler>();
        return services;
    }
}

internal sealed class OrphanFileCleanupWorker(IServiceScopeFactory scopes, SupportOptions options,
    ILogger<OrphanFileCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<OrphanFileCleanup>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Sem nomes, caminhos privados ou detalhes de ligação nos registos.
                logger.LogWarning("A limpeza de anexos órfãos falhou; será repetida na próxima execução.");
            }
        }
    }
}
