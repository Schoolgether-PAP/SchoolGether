using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc.Controllers;
using SchoolGether.Application.Onboarding;
using SchoolGether.Infrastructure.Onboarding;

namespace SchoolGether.Api.Onboarding;

internal static class OnboardingRegistration
{
    private static bool IsSignup(HttpContext context) => context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerTypeInfo.AsType() == typeof(OnboardingController);
    internal static IServiceCollection AddApiOnboarding(this IServiceCollection services)
    {
        services.AddOnboardingModule();
        services.AddScoped(provider => new OnboardingOptions { Enabled = provider.GetRequiredService<IConfiguration>().GetValue<bool>("Onboarding:Enabled") });
        services.AddExceptionHandler<OnboardingExceptionHandler>();
        services.AddRateLimiter(options =>
        {
            // O middleware encadeia os limites globais com o limite Identity existente.
            // Apenas o endereço efetivo do alojamento é usado; não lemos X-Forwarded-For.
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context => IsSignup(context)
                    ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local",
                        _ => Window(3)) : RateLimitPartition.GetNoLimiter("other")),
                PartitionedRateLimiter.Create<HttpContext, string>(context => IsSignup(context)
                    ? RateLimitPartition.GetFixedWindowLimiter("onboarding", _ => Window(20))
                    : RateLimitPartition.GetNoLimiter("other")));
        });
        return services;
    }
    internal static IApplicationBuilder UseOnboardingBodyLimit(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (!IsSignup(context)) { await next(context); return; }
        const int maximum = 16 * 1024;
        if (context.Request.ContentLength > maximum)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        // Limite também para corpos sem Content-Length, sem guardar credenciais num ficheiro temporário.
        var buffer = new byte[maximum + 1];
        var originalBody = context.Request.Body;
        var length = 0;
        try
        {
            while (length < buffer.Length)
            {
                var read = await originalBody.ReadAsync(buffer.AsMemory(length), context.RequestAborted);
                if (read == 0) break;
                length += read;
            }
            if (length > maximum)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }
            using var boundedBody = new MemoryStream(buffer, 0, length, writable: false);
            context.Request.Body = boundedBody;
            await next(context);
        }
        finally
        {
            context.Request.Body = originalBody;
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(buffer);
        }
    });

    private static FixedWindowRateLimiterOptions Window(int limit) => new()
    {
        PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
    };
}
