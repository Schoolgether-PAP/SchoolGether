using System.Net.Http.Json;
namespace SchoolGether.Api.IntegrationTests;
internal static class SupportHttpTestExtensions
{
    internal static async Task<HttpResponseMessage> PostSupportAsync<T>(this HttpClient client, string route, T request)
    {
        if (!route.StartsWith(SupportAuthorizationTests.Route, StringComparison.Ordinal) || route.EndsWith("/close", StringComparison.Ordinal))
            return await client.PostAsJsonAsync(route, request);
        using var body = SupportAttachmentTests.Multipart(request!);
        return await client.PostAsync(route, body);
    }
}
