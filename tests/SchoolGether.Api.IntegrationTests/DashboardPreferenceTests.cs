using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolGether.Api.IntegrationTests;

public sealed class DashboardPreferenceTests
{
    internal const string Root = "/api/v1/platform/dashboard/preferences";
    internal static object Layout(long version, string id = "institutions-status", int schema = 1, string size = "small") => new {
        SchemaVersion = schema, RowVersion = version, Widgets = new[] { new { Id = id, Size = size, Visible = true } } };

    [Fact]
    public async Task Preferences_AreIndividualPersistAcrossRequests_AndStaleVersionsConflict()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        await PlatformAdminTests.Created(master, "other@example.test", "institutions.read");
        using var other = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(other, "other@example.test");
        var initial = await master.GetFromJsonAsync<JsonElement>(Root);
        Assert.Equal(0, initial.GetProperty("rowVersion").GetInt64());
        Assert.Equal(new[] { "wide", "small", "full" }, initial.GetProperty("widgets").EnumerateArray().Select(x => x.GetProperty("size").GetString()));
        using var saved = await master.PutAsJsonAsync(Root, Layout(0)); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var savedOther = await other.PutAsJsonAsync(Root, Layout(0, "institutions-trend")); Assert.Equal(HttpStatusCode.OK, savedOther.StatusCode);
        using var reload = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(reload);
        var actual = await reload.GetFromJsonAsync<JsonElement>(Root + "?accountId=999");
        Assert.Equal("institutions-status", actual.GetProperty("widgets")[0].GetProperty("id").GetString());
        Assert.Equal(1, actual.GetProperty("rowVersion").GetInt64());
        var distinct = await other.GetFromJsonAsync<JsonElement>(Root);
        Assert.Equal("institutions-trend", distinct.GetProperty("widgets")[0].GetProperty("id").GetString());
        using var stale = await master.PutAsJsonAsync(Root, Layout(0)); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var next = await master.PutAsJsonAsync(Root, new { SchemaVersion = 1, RowVersion = 1, Widgets = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Empty((await next.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("widgets").EnumerateArray());
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "admin-a@example.test");
        using var denied = await school.GetAsync(Root); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("unknown")]
    [InlineData("width")]
    [InlineData("duplicate")]
    [InlineData("limit")]
    [InlineData("null")]
    [InlineData("null-item")]
    [InlineData("negative")]
    public async Task Preferences_RejectInvalidDocument(string kind)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client);
        object?[] widgets = [new { Id = "institutions-status", Size = "small", Visible = true }];
        if (kind == "unknown") widgets = [new { Id = "secret", Size = "small", Visible = true }];
        if (kind == "width") widgets = [new { Id = "institutions-status", Size = "huge", Visible = true }];
        if (kind == "duplicate") widgets = [widgets[0], widgets[0]];
        if (kind == "limit") widgets = [widgets[0], widgets[0], widgets[0], widgets[0]];
        if (kind == "null-item") widgets = [null];
        using var invalid = await client.PutAsJsonAsync(Root, new { SchemaVersion = kind == "schema" ? 2 : 1,
            RowVersion = kind == "negative" ? -1 : 0, Widgets = kind == "null" ? null : widgets });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var unchanged = await client.GetFromJsonAsync<JsonElement>(Root);
        Assert.Equal(0, unchanged.GetProperty("rowVersion").GetInt64());
    }
}
