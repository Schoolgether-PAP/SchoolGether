using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SchoolGether.Application.Identity;
using SchoolGether.Infrastructure.Identity;
using SchoolGether.Infrastructure.Institutions;
using SchoolGether.Infrastructure.Audit;
using SchoolGether.Infrastructure.Platform;
using SchoolGether.Api.Platform;
using Microsoft.AspNetCore.Authorization;

namespace SchoolGether.Api.Identity;

internal static class IdentityRegistration
{
    internal static IServiceCollection AddApiIdentity(this IServiceCollection services)
    {
        services.AddIdentityModule();
        services.AddInstitutionsModule();
        services.AddAuditModule();
        services.AddPlatformModule();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentAccount, CurrentAccount>();
        services.AddExceptionHandler<IdentityExceptionHandler>();
        services.AddExceptionHandler<PlatformExceptionHandler>();
        services.AddScoped<AdministrativeRefusalAudit>();
        services.AddScoped<AdministrativeValidationAuditFilter>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditedAuthorizationResultHandler>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<TokenSigningKey>((options, key) =>
            {
                options.Events = new JwtBearerEvents { OnTokenValidated = async context =>
                {
                    // Cada pedido escolar consulta a elegibilidade atual, incluindo suspensão e lockout.
                    if (context.Principal?.FindFirst("role")?.Value == "PlatformAdmin") return;
                    var id = int.TryParse(context.Principal?.FindFirst("sub")?.Value, out var parsedId) ? parsedId : 0;
                    var institutionId = int.TryParse(context.Principal?.FindFirst("institution_id")?.Value, out var parsedInstitution) ? parsedInstitution : 0;
                    var account = await context.HttpContext.RequestServices.GetRequiredService<IIdentityStore>()
                        .FindForAuthenticationAsync(id, context.HttpContext.RequestAborted);
                    if (account is null || account.InstitutionId != institutionId || account.Role.ToString() != context.Principal?.FindFirst("role")?.Value)
                        context.Fail("InvalidSession");
                } };
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters {
                    ValidateIssuerSigningKey = true, IssuerSigningKey = key.Key,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidateIssuer = true, ValidIssuer = TokenSigningKey.Issuer,
                    ValidateAudience = true, ValidAudience = TokenSigningKey.Audience,
                    ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(15), NameClaimType = "sub", RoleClaimType = "role" };
            }).ValidateOnStart();
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("identity", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0, AutoReplenishment = true }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext { HttpContext = context.HttpContext,
                        ProblemDetails = new() { Status = StatusCodes.Status429TooManyRequests } });
            };
        });
        services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Components ??= new();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" };
            foreach (var path in document.Paths)
                foreach (var operation in path.Value.Operations ?? [])
                    if (path.Key is "/api/v1/identity/me" or "/api/v1/identity/users" or "/api/v1/institutions" or "/api/v1/institutions/current/setup" or "/api/v1/institutions/current/subscription" ||
                        path.Key.StartsWith("/api/v1/platform/", StringComparison.Ordinal))
                        operation.Value.Security = [new OpenApiSecurityRequirement {
                            [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            return Task.CompletedTask;
        }));
        return services;
    }
}
