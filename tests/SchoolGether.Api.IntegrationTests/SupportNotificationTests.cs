using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using SchoolGether.Application.Support;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class SupportNotificationTests
{
    private const string Route = SupportAuthorizationTests.Route;

    [Fact]
    public async Task TwoClients_ReceiveCommittedPublicChanges_InternalNotesStayGlobal_AndRecoverAfterInterruption()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true); await factory.SeedAsync();
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "teacher-a@example.test");
        using var admin = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(admin);
        await using var global = new SupportLiveTestClient(factory, admin, HttpTransportType.WebSockets);
        await global.StartAsync();
        var ticket = await SupportAuthorizationTests.Ticket(school); var id = ticket.GetProperty("id").GetInt32();
        AssertEvent(await global.NextAsync(), id, ticket.GetProperty("rowVersion").GetInt64());
        await using var customer = new SupportLiveTestClient(factory, school);
        await customer.StartAsync(); await customer.JoinAsync(id); await global.JoinAsync(id);
        var initial = (await school.GetFromJsonAsync<JsonElement>($"{Route}/{id}/messages")).GetProperty("items")[0].GetProperty("id").GetInt32();
        var note = await SendAsync(admin, id, "INTERNAL-SENTINEL", true);
        var current = await admin.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
        AssertEvent(await global.NextAsync(), id, current.GetProperty("rowVersion").GetInt64());
        await customer.AssertQuietAsync();
        var publicMessage = await SendAsync(admin, id, "Resposta pública", false);
        current = await school.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
        var expectedVersion = current.GetProperty("rowVersion").GetInt64();
        AssertEvent(await customer.NextAsync(), id, expectedVersion); AssertEvent(await global.NextAsync(), id, expectedVersion);
        // A inscrição na fila e no pedido não duplica o mesmo aviso.
        await global.AssertQuietAsync();
        await customer.Connection.StopAsync();
        await SendAsync(admin, id, "Durante interrupção", false); await global.NextAsync();
        current = await admin.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
        using var closed = await admin.PostSupportAsync($"{Route}/{id}/close", new { RowVersion = current.GetProperty("rowVersion").GetInt64(), Summary = "Resolvido durante interrupção" });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode); await global.NextAsync();
        await customer.StartAsync(); await customer.JoinAsync(id);
        var recovered = await school.GetFromJsonAsync<JsonElement>($"{Route}/{id}/messages?afterId={initial}");
        Assert.Equal(new[] { "Resposta pública", "Durante interrupção" }, recovered.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("text").GetString()));
        Assert.DoesNotContain("INTERNAL-SENTINEL", recovered.GetRawText());
        current = await school.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
        Assert.Equal("Closed", current.GetProperty("status").GetString()); Assert.False(current.GetProperty("canReply").GetBoolean());
    }

    [Theory]
    [InlineData("suspended")]
    [InlineData("deleted")]
    [InlineData("lockout")]
    [InlineData("role")]
    public async Task PreviouslyJoinedSchoolConnection_IsRevalidatedBeforeNotificationAndInvocation(string change)
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true); await factory.SeedAsync();
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "teacher-a@example.test");
        using var admin = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(admin);
        var id = (await SupportAuthorizationTests.Ticket(school)).GetProperty("id").GetInt32();
        await using var customer = new SupportLiveTestClient(factory, school, HttpTransportType.WebSockets);
        await customer.StartAsync(); await customer.JoinAsync(id);
        await using var eligible = new SupportLiveTestClient(factory, admin);
        await eligible.StartAsync(); await eligible.JoinAsync(id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            if (change == "suspended")
                (await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionA)).SetSuspension(true, "Teste");
            else if (change == "role")
                await db.Members.IgnoreQueryFilters().Where(x => x.Name == "teacher-a@example.test").ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Role, SchoolGether.Domain.Identity.AccountRole.Student));
            else
            {
                var user = await db.Users.SingleAsync(x => x.Email == "teacher-a@example.test");
                if (change == "deleted") user.DeletedAt = DateTime.UtcNow;
                else user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1);
            }
            await db.SaveChangesAsync();
        }
        await SendAsync(admin, id, "Depois da revogação", false);
        Assert.Equal(id, (await eligible.NextAsync()).GetProperty("ticketId").GetInt32());
        await customer.AssertQuietAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => customer.Connection.InvokeAsync("LeaveTicket", id));
    }

    [Fact]
    public async Task RevokedGlobalRead_RemovesQueueAndTicketSubscriptionsBeforeDelivery()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true); await factory.SeedAsync();
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        var reader = await PlatformAdminTests.Created(master, "reader@example.test", "support.read");
        using var admin = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(admin, "reader@example.test");
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "teacher-a@example.test");
        var id = (await SupportAuthorizationTests.Ticket(school)).GetProperty("id").GetInt32();
        await using var live = new SupportLiveTestClient(factory, admin, HttpTransportType.WebSockets);
        await live.StartAsync(); await live.JoinAsync(id);
        using var revoked = await master.PutAsJsonAsync("/api/v1/platform/admins/" + reader.GetProperty("id").GetInt32(), new {
            Name = "Administrador", IsActive = true, Permissions = Array.Empty<string>(), RowVersion = reader.GetProperty("rowVersion").GetInt64(), Reason = "Revogar leitura" });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        await SendAsync(master, id, "Nota após revogação", true); await live.AssertQuietAsync();
        await SupportAuthorizationTests.Ticket(school); await live.AssertQuietAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => live.JoinAsync(id));
    }

    [Fact]
    public async Task NotifierFailure_DoesNotTurnCommittedHttpWriteIntoFailure()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISupportNotifier>(); services.AddSingleton<ISupportNotifier, FailingNotifier>();
        }));
        await factory.SeedAsync();
        using var client = configured.CreateClient(); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Route}/{ticket.GetProperty("id").GetInt32()}")).StatusCode);
    }

    internal static async Task<JsonElement> SendAsync(HttpClient client, int id, string text, bool internalMessage)
    {
        var ticket = await client.GetFromJsonAsync<JsonElement>($"{Route}/{id}");
        using var response = await client.PostSupportAsync($"{Route}/{id}/messages", new { ClientId = Guid.NewGuid(), RowVersion = ticket.GetProperty("rowVersion").GetInt64(), Text = text, Internal = internalMessage });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static void AssertEvent(JsonElement value, int id, long version)
    {
        Assert.Equal(2, value.EnumerateObject().Count());
        Assert.Equal(id, value.GetProperty("ticketId").GetInt32());
        Assert.Equal(version, value.GetProperty("rowVersion").GetInt64());
    }
    private sealed class FailingNotifier : ISupportNotifier
    { public Task ChangedAsync(int ticketId, bool internalOnly, CancellationToken ct) => throw new IOException("ControlledTransportFailure"); }

    [Theory]
    [InlineData("JoinTicket", false)]
    [InlineData("LeaveTicket", false)]
    [InlineData("JoinTicket", true)]
    [InlineData("LeaveTicket", true)]
    public async Task RevocationBeforeAnyNotification_IsCheckedOnEachInvocation(string method, bool global)
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true); await factory.SeedAsync();
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        var reader = await PlatformAdminTests.Created(master, "reader@example.test", "support.read");
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "teacher-a@example.test");
        var id = (await SupportAuthorizationTests.Ticket(school)).GetProperty("id").GetInt32();
        using var account = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(account, global ? "reader@example.test" : "teacher-a@example.test");
        await using var live = new SupportLiveTestClient(factory, account, HttpTransportType.WebSockets);
        await live.StartAsync(); await live.JoinAsync(id);
        if (global)
        {
            using var revoked = await master.PutAsJsonAsync("/api/v1/platform/admins/" + reader.GetProperty("id").GetInt32(), new {
                Name = "Administrador", IsActive = true, Permissions = Array.Empty<string>(), RowVersion = reader.GetProperty("rowVersion").GetInt64(), Reason = "Revogar leitura" });
            Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        }
        else
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == "teacher-a@example.test"); user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1); await db.SaveChangesAsync();
        }
        await Assert.ThrowsAnyAsync<Exception>(() => live.Connection.InvokeAsync(method, id));
    }

    [Fact]
    public async Task LeaveReplayAndFailedWrite_DoNotEmitSpuriousChanges()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true); await factory.SeedAsync();
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(school); var id = ticket.GetProperty("id").GetInt32();
        await using var live = new SupportLiveTestClient(factory, school);
        await live.StartAsync(); await live.JoinAsync(id);
        var command = new { ClientId = Guid.NewGuid(), RowVersion = ticket.GetProperty("rowVersion").GetInt64(), Text = "Confirmada", Internal = false };
        using var sent = await school.PostSupportAsync($"{Route}/{id}/messages", command);
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode); await live.NextAsync();
        using var replay = await school.PostSupportAsync($"{Route}/{id}/messages", command);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); await live.AssertQuietAsync();
        using var failed = await school.PostSupportAsync($"{Route}/{id}/messages", command with { ClientId = Guid.NewGuid(), Text = "Conflito" });
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode); await live.AssertQuietAsync();
        await live.Connection.InvokeAsync("LeaveTicket", id);
        await SendAsync(school, id, "Depois de sair", false); await live.AssertQuietAsync();
    }
}
