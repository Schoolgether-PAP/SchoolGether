using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Academic;

namespace SchoolGether.ApiClient.Academic;

public sealed class AcademicClassApiClient(HttpClient http, IdentitySession session)
{
    private static string Path(int courseId) => $"api/v1/academic/courses/{courseId}/classes";
    public Task<AcademicClassPageResponse> ListAsync(int courseId, int page, string search, bool archived, int? studyYear = null, string? academicYear = null, CancellationToken ct = default)
        => SendAsync<AcademicClassPageResponse>(HttpMethod.Get, $"{Path(courseId)}?page={page}&pageSize=20&search={Uri.EscapeDataString(search)}&status={(archived ? "Archived" : "Active")}{(studyYear.HasValue ? $"&studyYear={studyYear.Value}" : "")}{(!string.IsNullOrWhiteSpace(academicYear) ? $"&academicYear={Uri.EscapeDataString(academicYear)}" : "")}", null, ct);
    public Task<AcademicClassResponse> GetAsync(int courseId, int id, CancellationToken ct = default) => SendAsync<AcademicClassResponse>(HttpMethod.Get, $"{Path(courseId)}/{id}", null, ct);
    public Task<AcademicClassResponse> CreateAsync(int courseId, CreateAcademicClassRequest body, CancellationToken ct = default) => SendAsync<AcademicClassResponse>(HttpMethod.Post, Path(courseId), body, ct);
    public Task<AcademicClassResponse> UpdateAsync(int courseId, int id, UpdateAcademicClassRequest body, CancellationToken ct = default) => SendAsync<AcademicClassResponse>(HttpMethod.Put, $"{Path(courseId)}/{id}", body, ct);
    public Task<AcademicClassResponse> ArchiveAsync(int courseId, int id, long rowVersion, CancellationToken ct = default) => SendAsync<AcademicClassResponse>(HttpMethod.Post, $"{Path(courseId)}/{id}/archive", new AcademicClassStateRequest(rowVersion), ct);
    public Task<AcademicClassResponse> RestoreAsync(int courseId, int id, long rowVersion, CancellationToken ct = default) => SendAsync<AcademicClassResponse>(HttpMethod.Post, $"{Path(courseId)}/{id}/restore", new AcademicClassStateRequest(rowVersion), ct);

    private Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
        => session.ExecuteAuthorizedAsync(async (token, operationToken) =>
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, operationToken);
            return await ApiResponseReader.ReadAsync<T>(response, operationToken);
        }, ct);
}
