using System.Net;
using System.Text.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.ApiClient.Learning;
using SchoolGether.Contracts.Learning;
namespace SchoolGether.ApiClient.Tests;
public sealed class LessonPlansApiClientTests
{
    [Fact] public async Task Create_and_update_send_stable_key_and_version_without_tenant()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var key = Guid.NewGuid(); var date = new DateOnly(2026, 11, 25);
        f.Respond = (_, _) => Task.FromResult(SessionTestFixture.Json(new LessonPlanResponse(4, key, 1, 2, 3, "2026/2027", "Aula", date, "O", "C", "R", false, 2)));
        var api = new LessonPlansApiClient(f.Http, f.Session);
        var result = await api.CreateAsync(new(key, 1, 2, 3, "2026/2027", "Aula", date, "O", "C", "R")); Assert.Equal(key, result.ClientId);
        var create = Assert.Single(f.Requests, x => x.Path.EndsWith("lesson-plans")); using var json = JsonDocument.Parse(create.Body!); Assert.Equal(key, json.RootElement.GetProperty("clientId").GetGuid()); Assert.False(json.RootElement.TryGetProperty("institutionId", out _));
        await api.UpdateAsync(4, new(2, "Aula", date, "O", "C", "R")); var update = Assert.Single(f.Requests, x => x.Method == "PUT"); using var body = JsonDocument.Parse(update.Body!); Assert.Equal(2, body.RootElement.GetProperty("rowVersion").GetInt64());
        f.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)); var ex = await Assert.ThrowsAsync<IdentityApiException>(() => api.ArchiveAsync(4, 1)); Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
    }
}
