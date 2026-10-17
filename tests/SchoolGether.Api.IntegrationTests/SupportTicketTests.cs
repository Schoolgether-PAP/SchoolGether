using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolGether.Api.IntegrationTests;

public sealed class SupportTicketTests
{
    private const string Route = SupportAuthorizationTests.Route;

    [Fact]
    public async Task ActiveListGroupsNonClosedStatesBeforeCountingAndPaging()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "teacher-a@example.test");
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        var expected = new List<int>();
        foreach (var status in new[] { "Open", "Closed", "InProgress", "AwaitingClient", "Closed" })
        {
            var ticket = await SupportAuthorizationTests.Ticket(school); var id = ticket.GetProperty("id").GetInt32();
            var version = ticket.GetProperty("rowVersion").GetInt64();
            if (status == "Closed") Assert.Equal(HttpStatusCode.OK, (await master.PostAsJsonAsync($"{Route}/{id}/close", new { RowVersion = version, Summary = "Resolvido" })).StatusCode);
            else
            {
                expected.Insert(0, id);
                if (status != "Open") Assert.Equal(HttpStatusCode.OK, (await master.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = version, Status = status, Priority = "Normal", AssigneeId = (int?)null })).StatusCode);
            }
        }
        var first = await master.GetAsync($"{Route}?status=Active&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var page = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, page.GetProperty("total").GetInt32());
        Assert.Equal(expected.Take(2), page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()));
        page = await master.GetFromJsonAsync<JsonElement>($"{Route}?status=Active&pageSize=2&page=2");
        Assert.Equal(3, page.GetProperty("total").GetInt32());
        Assert.Equal(expected.Skip(2), page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()));
        Assert.Equal(2, (await master.GetFromJsonAsync<JsonElement>($"{Route}?status=Closed")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await master.GetFromJsonAsync<JsonElement>($"{Route}?status=InProgress")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await master.GetAsync($"{Route}?status=Unsupported")).StatusCode);
    }
    [Fact]
    public async Task PersistedDatesRemainUtc_AndCursorPagesDoNotSkipPublicMessages()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(client); var id = ticket.GetProperty("id").GetInt32();
        var loaded = await client.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
        Assert.Equal(DateTimeKind.Utc, loaded.GetProperty("createdAt").GetDateTime().Kind);
        var first = (await client.GetFromJsonAsync<JsonElement>($"{Route}/{id}/messages?take=1")).GetProperty("items")[0];
        Assert.Equal(DateTimeKind.Utc, first.GetProperty("createdAt").GetDateTime().Kind);
        var initialId = first.GetProperty("id").GetInt32();
        for (var i = 0; i < 3; i++)
        {
            loaded = await client.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
            Assert.Equal(HttpStatusCode.Created, (await client.PostSupportAsync($"{Route}/{id}/messages", new { ClientId = Guid.NewGuid(), RowVersion = loaded.GetProperty("rowVersion").GetInt64(), Text = "Mensagem " + i, Internal = false })).StatusCode);
        }
        var after = await client.GetFromJsonAsync<JsonElement>($"{Route}/{id}/messages?afterId={initialId}&take=2");
        Assert.True(after.GetProperty("hasMore").GetBoolean());
        Assert.Equal(new[] { "Mensagem 0", "Mensagem 1" }, after.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("text").GetString()));
        var newest = await client.GetFromJsonAsync<JsonElement>($"{Route}/{id}/messages?take=1");
        var lastId = newest.GetProperty("items")[0].GetProperty("id").GetInt32();
        var before = await client.GetFromJsonAsync<JsonElement>($"{Route}/{id}/messages?beforeId={lastId}&take=2");
        Assert.Equal(new[] { "Mensagem 0", "Mensagem 1" }, before.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("text").GetString()));
    }

    [Theory]
    [InlineData("institutionId", 999)]
    [InlineData("authorId", 999)]
    [InlineData("role", "PlatformAdmin")]
    [InlineData("files", "unexpected-file")]
    public async Task MetadataOnlyCreation_RejectsUnrecognizedFields(string field, object value)
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var payload = new Dictionary<string, object> { ["clientId"] = Guid.NewGuid(), ["subject"] = "Pedido", ["category"] = "Technical", ["priority"] = "Normal", ["description"] = "Descrição", [field] = value };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostSupportAsync(Route, payload)).StatusCode);
    }

    [Fact]
    public async Task RevokingManageAffectsCurrentTokenAndAssigneeEligibility()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        var manager = await PlatformAdminTests.Created(master, "manager@example.test", "support.read", "support.manage");
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(school); var id = ticket.GetProperty("id").GetInt32(); var version = ticket.GetProperty("rowVersion").GetInt64();
        var assigned = await master.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = version, Status = "InProgress", Priority = "Normal", AssigneeId = manager.GetProperty("id").GetInt32() });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var updated = await assigned.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("Administrador", updated.GetProperty("assigneeName").GetString());
        using var operatorClient = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(operatorClient, "manager@example.test");
        Assert.Equal(HttpStatusCode.OK, (await master.PutAsJsonAsync("/api/v1/platform/admins/" + manager.GetProperty("id").GetInt32(), new {
            Name = "Administrador", IsActive = true, Permissions = new[] { "support.read" }, RowVersion = manager.GetProperty("rowVersion").GetInt64(), Reason = "Revogar gestão" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.PostSupportAsync($"{Route}/{id}/close", new { RowVersion = updated.GetProperty("rowVersion").GetInt64(), Summary = "Sem permissão" })).StatusCode);
        Assert.Single((await master.GetFromJsonAsync<JsonElement>("/api/v1/support/assignees")).EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await master.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = updated.GetProperty("rowVersion").GetInt64(), Status = "Open", Priority = "Normal", AssigneeId = manager.GetProperty("id").GetInt32() })).StatusCode);
    }

    [Fact]
    public async Task Conversation_ReplaysAfterClose_AndNeverLeaksInternalNotes()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var customer = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(customer, "teacher-a@example.test");
        using var admin = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(admin);
        var creationId = Guid.NewGuid(); var ticket = await SupportAuthorizationTests.Ticket(customer, creationId);
        var id = ticket.GetProperty("id").GetInt32(); var version = ticket.GetProperty("rowVersion").GetInt64();
        using var update = await admin.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = version, Status = "AwaitingClient", Priority = "High", AssigneeId = (int?)null });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        version = (await update.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rowVersion").GetInt64();
        var note = new { ClientId = Guid.NewGuid(), RowVersion = version, Text = "INTERNAL-ONLY", Internal = true };
        Assert.Equal(HttpStatusCode.Created, (await admin.PostSupportAsync($"{Route}/{id}/messages", note)).StatusCode);
        var afterNote = await admin.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
        var message = new { ClientId = Guid.NewGuid(), RowVersion = afterNote.GetProperty("rowVersion").GetInt64(), Text = "Resposta do cliente", Internal = false };
        Assert.Equal(HttpStatusCode.Created, (await customer.PostSupportAsync($"{Route}/{id}/messages", message)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostSupportAsync($"{Route}/{id}/messages", message)).StatusCode);
        var reopened = await customer.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
        Assert.Equal("Open", reopened.GetProperty("status").GetString());
        var schoolMessages = await customer.GetStringAsync($"{Route}/{id}/messages");
        Assert.DoesNotContain("INTERNAL-ONLY", schoolMessages);
        Assert.DoesNotContain("\"internal\"", schoolMessages);
        Assert.Contains("INTERNAL-ONLY", await admin.GetStringAsync($"{Route}/{id}/messages"));
        using var close = await admin.PostSupportAsync($"{Route}/{id}/close", new { RowVersion = reopened.GetProperty("rowVersion").GetInt64(), Summary = "Resolvido" });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.False((await close.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("canReply").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await customer.PostSupportAsync(Route, SupportAuthorizationTests.Create(creationId))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.PostSupportAsync($"{Route}/{id}/messages", message)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await customer.PostSupportAsync(Route, SupportAuthorizationTests.Create(creationId, "Payload diferente"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await customer.PostSupportAsync($"{Route}/{id}/messages", message with { Text = "Diferente" })).StatusCode);
    }

    [Fact]
    public async Task ListAndCursorPaging_AreValidatedAndBounded()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(client); await SupportAuthorizationTests.Ticket(client);
        var page = await client.GetFromJsonAsync<JsonElement>(Route + "?page=2&pageSize=1&status=Open&priority=Normal&search=ajuda");
        Assert.Equal(2, page.GetProperty("total").GetInt32()); Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        foreach (var query in new[] { "page=0", "pageSize=101", "status=Unknown", "priority=99" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + "?" + query)).StatusCode);
        var id = ticket.GetProperty("id").GetInt32();
        foreach (var query in new[] { "take=0", "take=51", "beforeId=1&afterId=2" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Route}/{id}/messages?{query}")).StatusCode);
        var messages = await client.GetFromJsonAsync<JsonElement>($"{Route}/{id}/messages?take=1");
        Assert.Equal(1, messages.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ReadOnlyOperatorCannotReplyOrClose_AndAssignmentRequiresActiveManager()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(client); var id = ticket.GetProperty("id").GetInt32(); var version = ticket.GetProperty("rowVersion").GetInt64();
        await PlatformAdminTests.Authenticate(client);
        var reader = await PlatformAdminTests.Created(client, "reader@example.test", "support.read");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = version, Status = "Open", Priority = "Normal", AssigneeId = reader.GetProperty("id").GetInt32() })).StatusCode);
        await PlatformAdminTests.Authenticate(client, "reader@example.test");
        var read = await client.GetFromJsonAsync<JsonElement>($"{Route}/{id}"); Assert.False(read.GetProperty("canReply").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostSupportAsync($"{Route}/{id}/messages", new { ClientId = Guid.NewGuid(), RowVersion = version, Text = "Recusado", Internal = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostSupportAsync($"{Route}/{id}/close", new { RowVersion = version, Summary = "Recusado" })).StatusCode);
    }
}
