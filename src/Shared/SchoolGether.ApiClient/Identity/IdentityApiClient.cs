using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolGether.Contracts.Identity;

namespace SchoolGether.ApiClient.Identity;

// A interface gráfica decide onde guardar a sessão e quando renovar os tokens.
public sealed class IdentityApiClient(HttpClient http)
{
    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/identity/login", request, cancellationToken);
        return await ApiResponseReader.ReadAsync<TokenResponse>(response, cancellationToken);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/identity/refresh", request, cancellationToken);
        return await ApiResponseReader.ReadAsync<TokenResponse>(response, cancellationToken);
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/identity/logout", request, cancellationToken);
        await ApiResponseReader.EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<AccountResponse> GetMeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/identity/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, cancellationToken);
        return await ApiResponseReader.ReadAsync<AccountResponse>(response, cancellationToken);
    }
}

public sealed class IdentityApiException(HttpStatusCode statusCode, string? title, string? traceId)
    : HttpRequestException(title, null, statusCode)
{
    public string? TraceId { get; } = traceId;
}
