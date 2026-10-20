using SchoolGether.ApiClient.Identity;

namespace SchoolGether.Web.Identity;

public sealed record ApiConfiguration(bool IsConfigured);

public static class MemorySessionRegistration
{
    public static IServiceCollection AddMemoryIdentitySession(this IServiceCollection services,
        string? baseUrl, bool isDevelopment)
    {
        var valid = Uri.TryCreate(baseUrl, UriKind.Absolute, out var address)
            && (address.Scheme == Uri.UriSchemeHttps || (isDevelopment && address.Scheme == Uri.UriSchemeHttp))
            && string.IsNullOrEmpty(address.UserInfo) && string.IsNullOrEmpty(address.Query)
            && string.IsNullOrEmpty(address.Fragment);
        // Uma configuração em falta nunca usa a origem dos ficheiros estáticos como API.
        var apiAddress = valid ? new Uri(address!.AbsoluteUri.TrimEnd('/') + "/") : null;
        services.AddSingleton(new ApiConfiguration(valid));
        services.AddScoped(_ => new HttpClient { BaseAddress = apiAddress });
        services.AddScoped<IdentityApiClient>();
        services.AddScoped<SchoolGether.ApiClient.Billing.PlanApiClient>();
        services.AddScoped<SchoolGether.ApiClient.Onboarding.OnboardingApiClient>();
        services.AddScoped<SchoolGether.ApiClient.Institutions.InstitutionApiClient>();
        services.AddScoped<SchoolGether.ApiClient.Academic.AcademicCourseApiClient>();
        services.AddScoped<SchoolGether.ApiClient.Academic.AcademicCourseUnitApiClient>();
        services.AddScoped<SchoolGether.ApiClient.Academic.AcademicClassApiClient>();
        services.AddScoped<SchoolGether.ApiClient.Platform.PlatformApiClient>();
        services.AddScoped<IRefreshTokenStore, MemoryRefreshTokenStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IdentitySession>();
        services.AddScoped<SchoolGether.ApiClient.Support.SupportApiClient>();
        services.AddScoped<SchoolGether.ApiClient.Support.SupportLiveClient>();
        services.AddScoped<SchoolGether.ApiClient.Support.SupportConversationState>();
        return services;
    }
}
