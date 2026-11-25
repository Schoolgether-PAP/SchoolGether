using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Learning;
namespace SchoolGether.ApiClient.Learning;
public sealed class LessonPlansApiClient(HttpClient http, IdentitySession session)
{
    const string Path = "api/v1/learning/lesson-plans";
    public Task<LessonPlanPageResponse> ListAsync(string year, int page = 1, CancellationToken ct = default) => Send<LessonPlanPageResponse>(HttpMethod.Get, $"{Path}?academicYear={Uri.EscapeDataString(year)}&page={page}", null, ct);
    public Task<LessonPlanResponse> CreateAsync(CreateLessonPlanRequest body, CancellationToken ct = default) => Send<LessonPlanResponse>(HttpMethod.Post, Path, body, ct);
    public Task<LessonPlanResponse> UpdateAsync(int id, UpdateLessonPlanRequest body, CancellationToken ct = default) => Send<LessonPlanResponse>(HttpMethod.Put, $"{Path}/{id}", body, ct);
    public Task<LessonPlanResponse> ArchiveAsync(int id, long version, CancellationToken ct = default) => Send<LessonPlanResponse>(HttpMethod.Post, $"{Path}/{id}/archive", new LessonPlanStateRequest(version), ct);
    Task<T> Send<T>(HttpMethod method, string path, object? body, CancellationToken ct) => session.ExecuteAuthorizedAsync(async (token, cancellation) => { using var request = new HttpRequestMessage(method, path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); if (body is not null) request.Content = JsonContent.Create(body); using var response = await http.SendAsync(request, cancellation); return await ApiResponseReader.ReadAsync<T>(response, cancellation); }, ct);
}
