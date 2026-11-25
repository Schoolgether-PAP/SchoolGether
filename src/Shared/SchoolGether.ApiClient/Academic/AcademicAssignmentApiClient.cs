using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Academic;

namespace SchoolGether.ApiClient.Academic;

public sealed class AcademicAssignmentApiClient(HttpClient http, IdentitySession session)
{
    private static string Path(int courseId, int classId) => $"api/v1/academic/courses/{courseId}/classes/{classId}/assignments";

    public Task<AcademicAssignmentPageResponse> ListAsync(int courseId, int classId, int page, string search, bool cancelled, CancellationToken ct = default)
        => SendAsync<AcademicAssignmentPageResponse>(HttpMethod.Get, $"{Path(courseId, classId)}?page={page}&pageSize=20&search={Uri.EscapeDataString(search)}&status={(cancelled ? "Cancelled" : "Active")}", null, ct);

    public Task<AcademicTeacherPageResponse> EligibleTeachersAsync(int courseId, int classId, int page, string search, CancellationToken ct = default)
        => SendAsync<AcademicTeacherPageResponse>(HttpMethod.Get, $"{Path(courseId, classId)}/eligible-teachers?page={page}&pageSize=20&search={Uri.EscapeDataString(search)}", null, ct);

    public Task<AcademicAssignmentResponse> GetAsync(int courseId, int classId, int id, CancellationToken ct = default)
        => SendAsync<AcademicAssignmentResponse>(HttpMethod.Get, $"{Path(courseId, classId)}/{id}", null, ct);

    public Task<AcademicAssignmentResponse> CreateAsync(int courseId, int classId, CreateAcademicAssignmentRequest body, CancellationToken ct = default)
        => SendAsync<AcademicAssignmentResponse>(HttpMethod.Post, Path(courseId, classId), body, ct);

    public Task<AcademicAssignmentResponse> CancelAsync(int courseId, int classId, int id, AcademicAssignmentStateRequest body, CancellationToken ct = default)
        => SendAsync<AcademicAssignmentResponse>(HttpMethod.Post, $"{Path(courseId, classId)}/{id}/cancel", body, ct);

    public Task<AcademicAssignmentResponse> RestoreAsync(int courseId, int classId, int id, AcademicAssignmentStateRequest body, CancellationToken ct = default)
        => SendAsync<AcademicAssignmentResponse>(HttpMethod.Post, $"{Path(courseId, classId)}/{id}/restore", body, ct);

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
