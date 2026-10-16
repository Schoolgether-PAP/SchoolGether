using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class SupportAuthorizationTests
{
    internal const string Route = "/api/v1/support/tickets";
    internal static object Create(Guid? clientId = null, string subject = "Pedido de ajuda") =>
        new { ClientId = clientId ?? Guid.NewGuid(), Subject = subject, Category = "Technical", Priority = "Normal", Description = "Não consigo abrir a atividade." };
    internal static async Task<JsonElement> Ticket(HttpClient client, Guid? clientId = null)
    {
        using var response = await client.PostSupportAsync(Route, Create(clientId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Disabled_ContextRemainsAvailable_AndRoutesReturn503()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory);
        await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var context = await client.GetFromJsonAsync<JsonElement>("/api/v1/support/context");
        Assert.False(context.GetProperty("enabled").GetBoolean());
        Assert.False(context.GetProperty("canOpen").GetBoolean());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task SchoolScope_UsesCurrentActorAndHidesOtherTeachersAndInstitutions()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
        await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var ticket = await Ticket(client); var id = ticket.GetProperty("id").GetInt32();
        Assert.True(ticket.GetProperty("canReply").GetBoolean());
        Assert.False(ticket.GetProperty("canManage").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Route}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = 2, Status = "InProgress", Priority = "High" })).StatusCode);
        await PlatformAdminTests.Authenticate(client, "admin-a@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Route}/{id}")).StatusCode);
        await Ticket(client);
        await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var list = await client.GetFromJsonAsync<JsonElement>(Route);
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        await PlatformAdminTests.Authenticate(client, "admin-b@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{id}/messages")).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>(Route)).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Route}?institutionId={factory.InstitutionA}")).StatusCode);
        await PlatformAdminTests.Authenticate(client, "student-a@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostSupportAsync(Route, Create())).StatusCode);
    }

    [Fact]
    public async Task GlobalCapabilities_AreExplicitAndManageRequiresRead()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostSupportAsync("/api/v1/platform/admins", PlatformAdminTests.Create("invalid@example.test", "support.manage"))).StatusCode);
        await PlatformAdminTests.Created(client, "none@example.test");
        await PlatformAdminTests.Created(client, "read@example.test", "support.read");
        await PlatformAdminTests.Created(client, "manage@example.test", "support.read", "support.manage");
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/v1/support/context")).GetProperty("canOpen").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostSupportAsync(Route, Create())).StatusCode);
        await PlatformAdminTests.Authenticate(client, "none@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Route)).StatusCode);
        await PlatformAdminTests.Authenticate(client, "read@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/support/assignees")).StatusCode);
        await PlatformAdminTests.Authenticate(client, "manage@example.test");
        var assignees = await client.GetFromJsonAsync<JsonElement>("/api/v1/support/assignees");
        Assert.Equal(2, assignees.GetArrayLength());
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("lockout")]
    [InlineData("suspended")]
    public async Task ExistingToken_IsRevalidatedOnEveryReadAndReplay(string revocation)
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var clientId = Guid.NewGuid(); await Ticket(client, clientId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            if (revocation == "suspended")
            {
                var institution = await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionA);
                institution.SetSuspension(true, "Teste de revogação");
            }
            else
            {
                var user = await db.Users.SingleAsync(x => x.Email == "teacher-a@example.test");
                if (revocation == "deleted") user.DeletedAt = DateTime.UtcNow;
                else user.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1);
            }
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostSupportAsync(Route, Create(clientId))).StatusCode);
    }
}
