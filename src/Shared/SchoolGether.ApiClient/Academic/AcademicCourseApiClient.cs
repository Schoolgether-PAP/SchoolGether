using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Academic;

namespace SchoolGether.ApiClient.Academic;

public sealed class AcademicCourseApiClient(HttpClient http, IdentitySession session)
{
    private const string Path = "api/v1/academic/courses";
    public Task<CoursePageResponse> ListAsync(int page, string search, bool archived, CancellationToken ct = default)
        => SendAsync<CoursePageResponse>(HttpMethod.Get, $"{Path}?page={page}&pageSize=20&search={Uri.EscapeDataString(search)}&status={(archived ? "Archived" : "Active")}", null, ct);
    public Task<CourseResponse> GetAsync(int id, CancellationToken ct = default) => SendAsync<CourseResponse>(HttpMethod.Get, $"{Path}/{id}", null, ct);
    public Task<CourseResponse> CreateAsync(CreateCourseRequest body, CancellationToken ct = default) => SendAsync<CourseResponse>(HttpMethod.Post, Path, body, ct);
    public Task<CourseResponse> UpdateAsync(int id, UpdateCourseRequest body, CancellationToken ct = default) => SendAsync<CourseResponse>(HttpMethod.Put, $"{Path}/{id}", body, ct);
    public Task<CourseResponse> ArchiveAsync(int id, CourseStateRequest body, CancellationToken ct = default) => SendAsync<CourseResponse>(HttpMethod.Post, $"{Path}/{id}/archive", body, ct);
    public Task<CourseResponse> RestoreAsync(int id, CourseStateRequest body, CancellationToken ct = default) => SendAsync<CourseResponse>(HttpMethod.Post, $"{Path}/{id}/restore", body, ct);

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
