using SchoolGether.Infrastructure.Support;
using SchoolGether.Application.Support;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SchoolGether.Api.Support;

public static class SupportRegistration
{
    public static IServiceCollection AddApiSupport(this IServiceCollection services)
    {
        services.AddSupportModule();
        services.AddSignalR(options => options.EnableDetailedErrors = false);
        services.AddSingleton<SupportConnectionRegistry>();
        services.RemoveAll<ISupportNotifier>();
        services.AddSingleton<ISupportNotifier, SupportNotifier>();
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

    public static IApplicationBuilder UseSupportLiveOriginGuard(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/v1/support/live") && context.Request.Headers.TryGetValue("Origin", out var origin))
        {
            var allowed = context.RequestServices.GetRequiredService<IConfiguration>().GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            if (origin.Count != 1 || !allowed.Contains(origin[0], StringComparer.OrdinalIgnoreCase))
            { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
        }
        await next(context);
    });

    public static IEndpointConventionBuilder MapSupportLive(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHub<SupportHub>("/api/v1/support/live", options =>
        {
            options.CloseOnAuthenticationExpiration = true;
            // O navegador usa LongPolling com Authorization; WebSockets nativos apenas com cabeçalho.
            options.Transports = HttpTransportType.LongPolling | HttpTransportType.WebSockets;
            options.AllowStatefulReconnects = false;
        });
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
