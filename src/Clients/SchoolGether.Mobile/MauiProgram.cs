using System.Reflection;
using Microsoft.Extensions.Logging;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Mobile.Identity;

namespace SchoolGether.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });

#if DEBUG
        builder.Logging.AddDebug();
#if ANDROID
        var baseUrl = "http://10.0.2.2:5265/";
#else
        var baseUrl = "http://localhost:5265/";
#endif
#else
        var baseUrl = typeof(MauiProgram).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(value => value.Key == "SchoolGetherApiBaseUrl")?.Value;
#endif
        var valid = Uri.TryCreate(baseUrl, UriKind.Absolute, out var address)
            && !string.IsNullOrWhiteSpace(address.Host) && string.IsNullOrEmpty(address.UserInfo)
            && string.IsNullOrEmpty(address.Query) && string.IsNullOrEmpty(address.Fragment)
#if DEBUG
            && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);
#else
            && address.Scheme == Uri.UriSchemeHttps;
#endif
        // Sem fallback em Release: a interface bloqueia pedidos e explica a configuração em falta.
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        if (valid) http.BaseAddress = new Uri(address!.AbsoluteUri.TrimEnd('/') + "/");
        builder.Services.AddSingleton(http);
        builder.Services.AddSingleton<IdentityApiClient>();
        builder.Services.AddSingleton<IRefreshTokenStore, SecureRefreshTokenStore>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IdentitySession>();
        builder.Services.AddSingleton(services => new IdentityViewModel(
            services.GetRequiredService<IdentitySession>(),
            action => MainThread.BeginInvokeOnMainThread(action),
            valid ? null : UiMessages.ConfigurationError));
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<AppShell>();
        return builder.Build();
    }
}
