using System.Net;
using System.Text.Json;
using SchoolGether.ApiClient.Identity;
using SchoolGether.ApiClient.Learning;
using SchoolGether.Contracts.Learning;
namespace SchoolGether.ApiClient.Tests;
public sealed class LearningMaterialsApiClientTests
{
    [Fact] public async Task Create_and_update_preserve_key_version_and_publish_state()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var key = Guid.NewGuid();
        f.Respond = (_, _) => Task.FromResult(SessionTestFixture.Json(new LearningMaterialResponse(4, key, 1, 2, 3, "2026/2027", "Ficha", "Texto", "https://example.org/", true, false, 2)));
        var api = new LearningMaterialsApiClient(f.Http, f.Session);
        var result = await api.CreateAsync(new(key, 1, 2, 3, "2026/2027", "Ficha", "Texto", "https://example.org/", true)); Assert.Equal(key, result.ClientId);
        var create = Assert.Single(f.Requests, x => x.Path.EndsWith("materials")); using var json = JsonDocument.Parse(create.Body!); Assert.Equal(key, json.RootElement.GetProperty("clientId").GetGuid()); Assert.False(json.RootElement.TryGetProperty("institutionId", out _));
        await api.UpdateAsync(4, new(2, "Ficha", "Texto", "https://example.org/", true)); var update = Assert.Single(f.Requests, x => x.Method == "PUT"); using var body = JsonDocument.Parse(update.Body!); Assert.Equal(2, body.RootElement.GetProperty("rowVersion").GetInt64()); Assert.True(body.RootElement.GetProperty("isPublished").GetBoolean());
        f.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)); var ex = await Assert.ThrowsAsync<IdentityApiException>(() => api.ArchiveAsync(4, 1)); Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
    }
}
