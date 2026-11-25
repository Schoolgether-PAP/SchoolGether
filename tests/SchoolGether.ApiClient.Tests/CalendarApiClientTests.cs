using System.Net;
using System.Text.Json;
using SchoolGether.ApiClient.Calendar;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Calendar;
namespace SchoolGether.ApiClient.Tests;

public sealed class CalendarApiClientTests
{
    [Fact]
    public async Task Dates_and_versions_are_serialized_and_authorized_without_status_or_tenant()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials);
        var start = DateTimeOffset.Parse("2027-03-28T10:00:00+01:00"); var end = start.AddHours(1);
        f.Respond = (_, _) => Task.FromResult(SessionTestFixture.Json(new EventResponse(3, "Visita", null, null, start, end, "Pending", 7, 7)));
        var api = new CalendarApiClient(f.Http, f.Session); var result = await api.UpdateAsync(3, new(6, "Visita", null, null, start, end));
        Assert.Equal(start, result.Start); var sent = Assert.Single(f.Requests, x => x.Path.Contains("calendar")); Assert.Equal("Bearer", sent.Scheme); Assert.Equal("PUT", sent.Method);
        using var json = JsonDocument.Parse(sent.Body!); Assert.Equal(6, json.RootElement.GetProperty("rowVersion").GetInt64()); Assert.Equal(start, json.RootElement.GetProperty("start").GetDateTimeOffset()); Assert.False(json.RootElement.TryGetProperty("status", out _)); Assert.False(json.RootElement.TryGetProperty("institutionId", out _));
        f.Respond = (_, _) => Task.FromResult(SessionTestFixture.Json(new CalendarPageResponse([], 0, 2, 30)));
        await api.ListAsync(start, end, "Pending", 2); var query = Assert.Single(f.Requests, x => x.Path.Contains("?from=")); Assert.Contains("%2B01%3A00", query.Path); Assert.Contains("page=2", query.Path);
    }
    [Fact]
    public async Task Conflict_is_preserved_and_not_retried()
    {
        using var f = new SessionTestFixture(); await f.Session.SignInAsync(SessionTestFixture.Credentials); f.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict));
        var ex = await Assert.ThrowsAsync<IdentityApiException>(() => new CalendarApiClient(f.Http, f.Session).ChangeAsync(3, 2, "approve")); Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode); Assert.Single(f.Requests, x => x.Path.Contains("calendar"));
    }
}
