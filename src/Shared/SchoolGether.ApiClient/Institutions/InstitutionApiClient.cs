using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Identity;
using SchoolGether.Contracts.Institutions;
using SchoolGether.Contracts.Billing;
namespace SchoolGether.ApiClient.Institutions;
public sealed class InstitutionApiClient(HttpClient http, IdentitySession session)
{
    public Task<InstitutionSetupResponse> GetSetupAsync(CancellationToken cancellationToken = default) => SendAsync<InstitutionSetupResponse>(HttpMethod.Get, "api/v1/institutions/current/setup", null, cancellationToken);
    public Task<InstitutionSetupResponse> SaveSetupAsync(SaveInstitutionSetupRequest request, CancellationToken cancellationToken = default) => SendAsync<InstitutionSetupResponse>(HttpMethod.Put, "api/v1/institutions/current/setup", request, cancellationToken);
    public Task<InstitutionSubscriptionResponse> GetSubscriptionAsync(CancellationToken cancellationToken = default) => SendAsync<InstitutionSubscriptionResponse>(HttpMethod.Get, "api/v1/institutions/current/subscription", null, cancellationToken);
    public Task<MemberPageResponse> GetMembersAsync(int page, int pageSize, CancellationToken cancellationToken = default) => SendAsync<MemberPageResponse>(HttpMethod.Get, $"api/v1/identity/users?page={page}&pageSize={pageSize}", null, cancellationToken);
    public Task<AccountResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default) => SendAsync<AccountResponse>(HttpMethod.Post, "api/v1/identity/users", request, cancellationToken);
    private Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken) => session.ExecuteAuthorizedAsync(async (token, operationToken) =>
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, operationToken);
        return await ApiResponseReader.ReadAsync<T>(response, operationToken);
    }, cancellationToken);
}
