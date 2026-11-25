using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Calendar;
namespace SchoolGether.ApiClient.Calendar;

public sealed class CalendarApiClient(HttpClient http, IdentitySession session)
{
    const string Path = "api/v1/calendar/events";
    public Task<CalendarPageResponse> ListAsync(DateTimeOffset from, DateTimeOffset to, string status = "Published", int page = 1, CancellationToken ct = default) => Send<CalendarPageResponse>(HttpMethod.Get, $"{Path}?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}&status={Uri.EscapeDataString(status)}&page={page}&pageSize=30", null, ct);
    public Task<EventResponse> CreateAsync(CreateEventRequest body, CancellationToken ct = default) => Send<EventResponse>(HttpMethod.Post, Path, body, ct);
    public Task<EventResponse> UpdateAsync(int id, UpdateEventRequest body, CancellationToken ct = default) => Send<EventResponse>(HttpMethod.Put, $"{Path}/{id}", body, ct);
    public Task<EventResponse> ChangeAsync(int id, long version, string operation, CancellationToken ct = default)
    { if (operation is not ("approve" or "reject" or "cancel")) throw new ArgumentException(nameof(operation)); return Send<EventResponse>(HttpMethod.Post, $"{Path}/{id}/{operation}", new EventStateRequest(version), ct); }
    Task<T> Send<T>(HttpMethod method, string path, object? body, CancellationToken ct) => session.ExecuteAuthorizedAsync(async (token, cancellation) => { using var request = new HttpRequestMessage(method, path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); if (body is not null) request.Content = JsonContent.Create(body); using var response = await http.SendAsync(request, cancellation); return await ApiResponseReader.ReadAsync<T>(response, cancellation); }, ct);
}
