using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Learning;
namespace SchoolGether.ApiClient.Learning;
public sealed class LearningMaterialsApiClient(HttpClient http, IdentitySession session)
{
    const string Path = "api/v1/learning/materials";
    public Task<LearningMaterialPageResponse> ListAsync(string year, int page = 1, CancellationToken ct = default) => Send<LearningMaterialPageResponse>(HttpMethod.Get, $"{Path}?academicYear={Uri.EscapeDataString(year)}&page={page}", null, ct);
    public Task<LearningMaterialResponse> CreateAsync(CreateLearningMaterialRequest body, CancellationToken ct = default) => Send<LearningMaterialResponse>(HttpMethod.Post, Path, body, ct);
    public Task<LearningMaterialResponse> UpdateAsync(int id, UpdateLearningMaterialRequest body, CancellationToken ct = default) => Send<LearningMaterialResponse>(HttpMethod.Put, $"{Path}/{id}", body, ct);
    public Task<LearningMaterialResponse> ArchiveAsync(int id, long version, CancellationToken ct = default) => Send<LearningMaterialResponse>(HttpMethod.Post, $"{Path}/{id}/archive", new LearningMaterialStateRequest(version), ct);
    Task<T> Send<T>(HttpMethod method, string path, object? body, CancellationToken ct) => session.ExecuteAuthorizedAsync(async (token, cancellation) => { using var request = new HttpRequestMessage(method, path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); if (body is not null) request.Content = JsonContent.Create(body); using var response = await http.SendAsync(request, cancellation); return await ApiResponseReader.ReadAsync<T>(response, cancellation); }, ct);
}
