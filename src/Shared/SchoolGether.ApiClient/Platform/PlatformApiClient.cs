using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Identity;
using SchoolGether.Contracts.Platform;

namespace SchoolGether.ApiClient.Platform;

public sealed class PlatformApiClient(HttpClient http, IdentitySession session)
{
    private const string Root = "api/v1/platform";

    public Task<PlatformContextResponse> GetContextAsync(CancellationToken cancellationToken = default) =>
        SendAsync<PlatformContextResponse>(HttpMethod.Get, $"{Root}/context", null, cancellationToken);

    public Task<PagedResponse<PlatformAdminResponse>> ListAdminsAsync(int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedResponse<PlatformAdminResponse>>(HttpMethod.Get,
            $"{Root}/admins?page={page}&pageSize={pageSize}", null, cancellationToken);

    public Task<PlatformAdminResponse> CreateAdminAsync(CreatePlatformAdminRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<PlatformAdminResponse>(HttpMethod.Post, $"{Root}/admins", request, cancellationToken);

    public Task<PlatformAdminResponse> UpdateAdminAsync(int id, UpdatePlatformAdminRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<PlatformAdminResponse>(HttpMethod.Put, $"{Root}/admins/{id}", request, cancellationToken);

    public Task<PagedResponse<AuditResponse>> ListAuditAsync(int page = 1, int pageSize = 20,
        string? search = null, int? institutionId = null, CancellationToken cancellationToken = default) =>
        SendAsync<PagedResponse<AuditResponse>>(HttpMethod.Get,
            $"{Root}/audit?page={page}&pageSize={pageSize}{TextQuery("search", search)}{ValueQuery("institutionId", institutionId)}",
            null, cancellationToken);

    public Task<PagedResponse<PlatformInstitutionResponse>> ListInstitutionsAsync(int page = 1,
        int pageSize = 20, string? search = null, bool? isSuspended = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedResponse<PlatformInstitutionResponse>>(HttpMethod.Get,
            $"{Root}/institutions?page={page}&pageSize={pageSize}{TextQuery("search", search)}{BooleanQuery("isSuspended", isSuspended)}",
            null, cancellationToken);

    public Task<PlatformInstitutionDetailResponse> GetInstitutionAsync(int id,
        CancellationToken cancellationToken = default) =>
        SendAsync<PlatformInstitutionDetailResponse>(HttpMethod.Get, $"{Root}/institutions/{id}", null, cancellationToken);

    public Task<PlatformInstitutionResponse> CreateInstitutionAsync(CreatePlatformInstitutionRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<PlatformInstitutionResponse>(HttpMethod.Post, $"{Root}/institutions", request, cancellationToken);

    public Task<PlatformInstitutionResponse> UpdateInstitutionAsync(int id, UpdatePlatformInstitutionRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<PlatformInstitutionResponse>(HttpMethod.Put, $"{Root}/institutions/{id}", request, cancellationToken);

    public Task<PlatformInstitutionResponse> SetInstitutionSuspensionAsync(int id, SetInstitutionSuspensionRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<PlatformInstitutionResponse>(HttpMethod.Put, $"{Root}/institutions/{id}/suspension", request, cancellationToken);

    public Task<AccountResponse> CreateInstitutionAdminAsync(int id, CreateInstitutionAdminRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<AccountResponse>(HttpMethod.Post, $"{Root}/institutions/{id}/administrators", request, cancellationToken);

    public Task<PlatformDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        SendAsync<PlatformDashboardResponse>(HttpMethod.Get, $"{Root}/dashboard", null, cancellationToken);

    public Task<DashboardPreferenceResponse> GetDashboardPreferencesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<DashboardPreferenceResponse>(HttpMethod.Get, $"{Root}/dashboard/preferences", null, cancellationToken);

    public Task<DashboardPreferenceResponse> SaveDashboardPreferencesAsync(SaveDashboardPreferenceRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<DashboardPreferenceResponse>(HttpMethod.Put, $"{Root}/dashboard/preferences", request, cancellationToken);

    private Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken) =>
        session.ExecuteAuthorizedAsync(async (token, operationToken) =>
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, operationToken);
            return await ApiResponseReader.ReadAsync<T>(response, operationToken);
        }, cancellationToken);

    private static string TextQuery(string name, string? value) =>
        value is null ? string.Empty : $"&{name}={Uri.EscapeDataString(value)}";

    private static string ValueQuery(string name, int? value) =>
        value is null ? string.Empty : $"&{name}={value.Value}";

    private static string BooleanQuery(string name, bool? value) =>
        value is null ? string.Empty : $"&{name}={value.Value.ToString().ToLowerInvariant()}";
}
