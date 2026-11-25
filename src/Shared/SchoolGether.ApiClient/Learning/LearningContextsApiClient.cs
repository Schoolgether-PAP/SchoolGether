using System.Net.Http.Headers;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Learning;
namespace SchoolGether.ApiClient.Learning;
public sealed class LearningContextsApiClient(HttpClient http, IdentitySession session)
{
    public Task<LearningContextResponse[]> ListAsync(string year, CancellationToken ct = default) => session.ExecuteAuthorizedAsync(async (token, cancellation) => { using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/learning/contexts?academicYear={Uri.EscapeDataString(year)}"); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); using var response = await http.SendAsync(request, cancellation); return await ApiResponseReader.ReadAsync<LearningContextResponse[]>(response, cancellation); }, ct);
}
