using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Academic;

namespace SchoolGether.ApiClient.Academic;

public sealed class AcademicCourseUnitApiClient(HttpClient http, IdentitySession session)
{
    private static string Path(int courseId) => $"api/v1/academic/courses/{courseId}/units";
    public Task<CourseUnitPageResponse> ListAsync(int courseId, int page, string search, bool archived, CancellationToken ct = default)
        => SendAsync<CourseUnitPageResponse>(HttpMethod.Get, $"{Path(courseId)}?page={page}&pageSize=20&search={Uri.EscapeDataString(search)}&status={(archived ? "Archived" : "Active")}", null, ct);
    public Task<CourseUnitResponse> GetAsync(int courseId, int id, CancellationToken ct = default) => SendAsync<CourseUnitResponse>(HttpMethod.Get, $"{Path(courseId)}/{id}", null, ct);
    public Task<CourseUnitResponse> CreateAsync(int courseId, CreateCourseUnitRequest body, CancellationToken ct = default) => SendAsync<CourseUnitResponse>(HttpMethod.Post, Path(courseId), body, ct);
    public Task<CourseUnitResponse> UpdateAsync(int courseId, int id, UpdateCourseUnitRequest body, CancellationToken ct = default) => SendAsync<CourseUnitResponse>(HttpMethod.Put, $"{Path(courseId)}/{id}", body, ct);
    public Task<CourseUnitResponse> ArchiveAsync(int courseId, int id, long rowVersion, CancellationToken ct = default) => SendAsync<CourseUnitResponse>(HttpMethod.Post, $"{Path(courseId)}/{id}/archive", new CourseUnitStateRequest(rowVersion), ct);
    public Task<CourseUnitResponse> RestoreAsync(int courseId, int id, long rowVersion, CancellationToken ct = default) => SendAsync<CourseUnitResponse>(HttpMethod.Post, $"{Path(courseId)}/{id}/restore", new CourseUnitStateRequest(rowVersion), ct);

    private Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
        => session.ExecuteAuthorizedAsync(async (token, operationToken) =>
        {
            // A renovação autorizada cria sempre um pedido e conteúdo novos.
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, operationToken);
            return await ApiResponseReader.ReadAsync<T>(response, operationToken);
        }, ct);
}
