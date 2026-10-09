using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolGether.Contracts.Identity;

namespace SchoolGether.ApiClient.Identity;

// A interface gráfica decide onde guardar a sessão e quando renovar os tokens.
public sealed class IdentityApiClient(HttpClient http)
{
    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/identity/login", request, cancellationToken);
        return await ReadAsync<TokenResponse>(response, cancellationToken);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/identity/refresh", request, cancellationToken);
        return await ReadAsync<TokenResponse>(response, cancellationToken);
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/identity/logout", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<AccountResponse> GetMeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/identity/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, cancellationToken);
        return await ReadAsync<AccountResponse>(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))
            ?? throw new HttpRequestException("EmptyApiResponse");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        string? title = null;
        string? traceId = null;
        if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (json.RootElement.TryGetProperty("title", out var titleValue)) title = titleValue.GetString();
            if (json.RootElement.TryGetProperty("traceId", out var traceValue)) traceId = traceValue.GetString();
        }
        throw new IdentityApiException(response.StatusCode, title, traceId);
    }
}

public sealed class IdentityApiException(HttpStatusCode statusCode, string? title, string? traceId)
    : HttpRequestException(title, null, statusCode)
{
    public string? TraceId { get; } = traceId;
}
