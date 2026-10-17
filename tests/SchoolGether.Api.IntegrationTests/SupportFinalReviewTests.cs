using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Contracts.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class SupportFinalReviewTests
{
    private const string Route = SupportAuthorizationTests.Route;

    [Theory]
    [InlineData("teacher-a@example.test")]
    [InlineData("admin-a@example.test")]
    public async Task SchoolParticipantClosesAuthorizedTicket_WithSummaryVersionAndTerminalGuards(string closer)
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(client); var id = ticket.GetProperty("id").GetInt32();
        var version = ticket.GetProperty("rowVersion").GetInt64();
        await PlatformAdminTests.Authenticate(client, closer);
        var loaded = await client.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
        Assert.True(loaded.GetProperty("canClose").GetBoolean()); Assert.False(loaded.GetProperty("canManage").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostSupportAsync($"{Route}/{id}/messages", new { ClientId = Guid.NewGuid(), RowVersion = version, Text = "Nota recusada", Internal = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Route}/{id}/close", new { RowVersion = version, Summary = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{Route}/{id}/close", new { RowVersion = version, Summary = new string('x', 1001) })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Route}/{id}/close", new { RowVersion = version - 1, Summary = "Resolvido" })).StatusCode);
        using var response = await client.PostAsJsonAsync($"{Route}/{id}/close", new { RowVersion = version, Summary = "Resolvido pela escola" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var closed = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Closed", closed.GetProperty("status").GetString()); Assert.Equal(version + 1, closed.GetProperty("rowVersion").GetInt64());
        Assert.False(closed.GetProperty("canClose").GetBoolean()); Assert.False(closed.GetProperty("canReply").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Route}/{id}/close", new { RowVersion = version + 1, Summary = "Repetir" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostSupportAsync($"{Route}/{id}/messages", new { ClientId = Guid.NewGuid(), RowVersion = version + 1, Text = "Depois", Internal = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = version + 1, Status = "Open", Priority = "High" })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var audit = Assert.Single(await db.AuditEntries.Where(x => x.Action == "support.close").ToListAsync());
        Assert.Equal(factory.InstitutionA, audit.InstitutionId); Assert.Equal(closed.GetProperty("closedBy").GetInt32(), audit.ActorId);
    }

    [Fact]
    public async Task SchoolCloseMasksForeignTickets_AndRevalidatesCurrentSession()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "admin-a@example.test");
        (await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest("colleague@example.test", IdentityFactory.Password, "Colega", "Teacher", null))).EnsureSuccessStatusCode();
        await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(client); var id = ticket.GetProperty("id").GetInt32();
        var command = new { RowVersion = ticket.GetProperty("rowVersion").GetInt64(), Summary = "Resolvido" };
        foreach (var (email, expected) in new[] { ("colleague@example.test", HttpStatusCode.NotFound), ("admin-b@example.test", HttpStatusCode.NotFound), ("student-a@example.test", HttpStatusCode.Forbidden) })
        {
            await PlatformAdminTests.Authenticate(client, email);
            Assert.Equal(expected, (await client.PostAsJsonAsync($"{Route}/{id}/close", command)).StatusCode);
        }
        await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            (await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionA)).SetSuspension(true, "Teste"); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"{Route}/{id}/close", command)).StatusCode);
    }

    [Fact]
    public async Task SearchIncludesExactDisplayedId_AlongsideSubject_WithinAuthorizedScope()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(client); var id = ticket.GetProperty("id").GetInt32();
        var byId = await client.GetFromJsonAsync<JsonElement>($"{Route}?search={id}&pageSize=1");
        Assert.Equal(1, byId.GetProperty("total").GetInt32()); Assert.Equal(id, byId.GetProperty("items")[0].GetProperty("id").GetInt32());
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>($"{Route}?search=ajuda")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>($"{Route}?search=2147483647")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>($"{Route}?search={id}&page=2&pageSize=1")).GetProperty("items").GetArrayLength());
        await PlatformAdminTests.Authenticate(client, "admin-b@example.test");
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>($"{Route}?search={id}")).GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("permission")]
    [InlineData("deleted")]
    [InlineData("unknown")]
    public async Task UnavailableAssigneeRemainsVisibleUntilExplicitReassignment(string change)
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(school); var id = ticket.GetProperty("id").GetInt32();
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        var manager = await PlatformAdminTests.Created(master, "manager@example.test", "support.read", "support.manage"); var assignee = manager.GetProperty("id").GetInt32();
        using var assigned = await master.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = ticket.GetProperty("rowVersion").GetInt64(), Status = "InProgress", Priority = "Normal", AssigneeId = assignee }); assigned.EnsureSuccessStatusCode();
        Assert.True((await assigned.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assigneeAvailable").GetBoolean());
        using var revoke = await master.PutAsJsonAsync($"/api/v1/platform/admins/{assignee}", new { Name = "Operador", IsActive = change != "inactive", Permissions = change == "permission" ? new[] { "support.read" } : new[] { "support.read", "support.manage" }, RowVersion = manager.GetProperty("rowVersion").GetInt64(), Reason = "Teste" }); revoke.EnsureSuccessStatusCode();
        if (change is "deleted" or "unknown")
        {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            if (change == "deleted") { (await db.Users.SingleAsync(x => x.Id == assignee)).DeletedAt = DateTime.UtcNow; await db.SaveChangesAsync(); }
            else { assignee = int.MaxValue; await db.SupportTickets.IgnoreQueryFilters().Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AssigneeId, assignee)); }
        }
        foreach (var client in new[] { master, school })
        {
            var loaded = await client.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
            Assert.Equal(assignee, loaded.GetProperty("assigneeId").GetInt32()); Assert.Equal(change == "unknown" ? null : "Operador", loaded.GetProperty("assigneeName").GetString()); Assert.False(loaded.GetProperty("assigneeAvailable").GetBoolean());
            var listed = (await client.GetFromJsonAsync<JsonElement>(Route)).GetProperty("items")[0]; Assert.False(listed.GetProperty("assigneeAvailable").GetBoolean());
        }
        ticket = await master.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
        using var unrelated = await master.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = ticket.GetProperty("rowVersion").GetInt64(), Status = "AwaitingClient", Priority = "High", AssigneeId = assignee }); unrelated.EnsureSuccessStatusCode();
        ticket = await unrelated.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(assignee, ticket.GetProperty("assigneeId").GetInt32()); Assert.False(ticket.GetProperty("assigneeAvailable").GetBoolean());
        using var clear = await master.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = ticket.GetProperty("rowVersion").GetInt64(), Status = "InProgress", Priority = "High", AssigneeId = (int?)null }); clear.EnsureSuccessStatusCode();
        ticket = await clear.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(JsonValueKind.Null, ticket.GetProperty("assigneeId").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await master.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = ticket.GetProperty("rowVersion").GetInt64(), Status = "Open", Priority = "Normal", AssigneeId = assignee })).StatusCode);
    }

    [Fact]
    public async Task DownloadsAndRelevantRefusalsAreAuditedWithoutContentOrQuery_ValidationIsNotARefusal()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "teacher-a@example.test");
        using var body = SupportAttachmentTests.Multipart(SupportAuthorizationTests.Create(), ("SECRET-FILENAME.txt", "SECRET-CONTENT"u8.ToArray()));
        using var created = await school.PostAsync(Route, body); created.EnsureSuccessStatusCode(); var ticket = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = ticket.GetProperty("id").GetInt32();
        var messages = await school.GetFromJsonAsync<JsonElement>($"{Route}/{id}/messages"); var file = messages.GetProperty("items")[0].GetProperty("attachments")[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await school.GetAsync($"/api/v1/support/attachments/{file}?secret=SECRET-QUERY")).StatusCode);
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        var reader = await PlatformAdminTests.Created(master, "reader@example.test", "support.read");
        using var noteBody = SupportAttachmentTests.Multipart(new { ClientId = Guid.NewGuid(), RowVersion = ticket.GetProperty("rowVersion").GetInt64(), Text = "SECRET-NOTE", Internal = true }, ("SECRET-INTERNAL.txt", "internal"u8.ToArray()));
        using var note = await master.PostAsync($"{Route}/{id}/messages", noteBody); note.EnsureSuccessStatusCode(); var internalFile = (await note.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attachments")[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await school.GetAsync($"/api/v1/support/attachments/{internalFile}")).StatusCode);
        await PlatformAdminTests.Authenticate(school, "admin-b@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await school.GetAsync($"/api/v1/support/attachments/{file}")).StatusCode);
        await PlatformAdminTests.Authenticate(school, "reader@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await school.PostAsJsonAsync($"{Route}/{id}/close", new { RowVersion = 1, Summary = "SECRET-SUMMARY" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await master.GetAsync(Route + "?page=0&search=SECRET-SEARCH")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var records = await db.AuditEntries.Where(x => x.Action.StartsWith("support.")).OrderBy(x => x.Id).ToListAsync();
        var download = Assert.Single(records, x => x.Action == "support.download"); Assert.Equal("success", download.Result); Assert.Equal("support.attachment:" + file, download.Target); Assert.Equal(factory.InstitutionA, download.InstitutionId);
        var refused = records.Where(x => x.Result == "refused").ToArray(); Assert.Equal(3, refused.Length); Assert.Equal(2, refused.Count(x => x.Reason == "SupportNotFound")); Assert.Single(refused, x => x.Reason == "Forbidden");
        Assert.Equal(new[] { "support.attachment:" + internalFile, "support.attachment:" + file, "support.ticket:" + id }, refused.Select(x => x.Target));
        Assert.DoesNotContain("SECRET-", JsonSerializer.Serialize(records));
    }
}
