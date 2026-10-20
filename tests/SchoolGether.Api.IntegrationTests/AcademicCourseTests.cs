using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SchoolGether.Contracts.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class AcademicCourseTests
{
    internal const string Route = "/api/v1/academic/courses";
    internal static async Task Login(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest(email, IdentityFactory.Password));
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }
    internal static async Task<JsonElement> Read(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        Assert.Equal(status, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Fact]
    public async Task Course_lifecycle_preserves_initial_replay_and_occupied_archived_code()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = factory.CreateClient(); await Login(client, "admin-a@example.test");
        var key = Guid.NewGuid(); var body = new { ClientId = key, Code = " ti_1 ", Name = " Informática ", Description = "  " };
        var created = await Read(await client.PostAsJsonAsync(Route, body), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetInt32();
        Assert.Equal("TI_1", created.GetProperty("code").GetString());
        Assert.Equal("Informática", created.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("description").ValueKind);
        Assert.False(created.TryGetProperty("creationFingerprint", out _));
        await Read(await client.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = 1, Code = "TI2", Name = "Novo", Description = "texto" }));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = 1, Code = "TI3", Name = "Perdido" })).StatusCode);
        var archived = await Read(await client.PostAsJsonAsync($"{Route}/{id}/archive", new { RowVersion = 2 }));
        Assert.False(archived.GetProperty("isActive").GetBoolean());
        var replay = await Read(await client.PostAsJsonAsync(Route, body));
        Assert.True(replay.GetProperty("replay").GetBoolean()); Assert.Equal("Novo", replay.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route, new { ClientId = key, Code = "DIFFERENT", Name = "Outro" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route, new { ClientId = Guid.NewGuid(), Code = "ti2", Name = "Ocupado" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"{Route}/{id}", new { RowVersion = 3, Code = "TI2", Name = "Vedado" })).StatusCode);
        var active = await Read(await client.GetAsync(Route)); Assert.Equal(0, active.GetProperty("total").GetInt32());
        var archive = await Read(await client.GetAsync(Route + "?status=Archived&search=novo")); Assert.Equal(1, archive.GetProperty("total").GetInt32());
        var restored = await Read(await client.PostAsJsonAsync($"{Route}/{id}/restore", new { RowVersion = 3 })); Assert.Equal(4, restored.GetProperty("rowVersion").GetInt64());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var audits = await db.AuditEntries.Where(x => x.Action.StartsWith("academic.") && x.Result == "success").ToListAsync();
        Assert.Equal(4, audits.Count); Assert.All(audits, x => Assert.Equal("academic.course:" + key.ToString("D"), x.Target));
    }
    [Fact]
    public async Task Routes_revalidate_identity_enforce_roles_tenant_and_strict_json()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
        foreach (var email in new[] { "teacher-a@example.test", "student-a@example.test", "platform@example.test" })
        { await Login(client, email); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Route)).StatusCode); }
        await Login(client, "admin-a@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, new { ClientId = Guid.NewGuid(), Code = "A", Name = "A", InstitutionId = factory.InstitutionB })).StatusCode);
        var created = await Read(await client.PostAsJsonAsync(Route, new { ClientId = Guid.NewGuid(), Code = "A", Name = "A" }), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetInt32(); await Login(client, "admin-b@example.test");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{Route}/{id}/archive", new { RowVersion = 1 })).StatusCode);
        Assert.Equal(0, (await Read(await client.GetAsync(Route))).GetProperty("total").GetInt32());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == "admin-b@example.test"); db.Entry(user).Property(x => x.IsActive).CurrentValue = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
    }
    [Fact]
    public async Task Normalized_limits_pagination_and_validation_are_applied_on_server()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = factory.CreateClient(); await Login(client, "admin-a@example.test");
        var first = await Read(await client.PostAsJsonAsync(Route, new { ClientId = Guid.NewGuid(), Code = " " + new string('a',30) + " ", Name = " " + new string('N',200) + " ", Description = " " + new string('D',1000) + " " }), HttpStatusCode.Created);
        for (var i = 1; i <= 3; i++) await Read(await client.PostAsJsonAsync(Route, new { ClientId = Guid.NewGuid(), Code = "C" + i, Name = "Curso " + i }), HttpStatusCode.Created);
        var page = await Read(await client.GetAsync(Route + "?page=2&pageSize=2")); Assert.Equal(4, page.GetProperty("total").GetInt32());
        Assert.Equal(first.GetProperty("id").GetInt32(), page.GetProperty("items")[1].GetProperty("id").GetInt32());
        Assert.Equal(3, (await Read(await client.GetAsync(Route + "?search=curso"))).GetProperty("total").GetInt32());
        foreach (var query in new[] { "page=0", "pageSize=101", "pageSize=0", "status=all", "page=2147483647&pageSize=100", "search=" + new string('X',151) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + "?" + query)).StatusCode);
        foreach (var code in new[] { "ÁBC", "_A", "A B", "" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, new { ClientId = Guid.NewGuid(), Code = code, Name = "Curso" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, new { ClientId = Guid.Empty, Code = "X", Name = "Curso" })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync(Route + "/" + first.GetProperty("id").GetInt32())).StatusCode);
    }
    [Theory]
    [InlineData("role")]
    [InlineData("tenant")]
    [InlineData("suspended")]
    public async Task Stale_claims_or_suspended_institution_cannot_read_or_replay(string change)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = factory.CreateClient(); await Login(client, "admin-a@example.test");
        var body = new { ClientId = Guid.NewGuid(), Code = "A", Name = "Curso" }; await Read(await client.PostAsJsonAsync(Route, body), HttpStatusCode.Created);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            if (change == "suspended") (await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionA)).SetSuspension(true, "Teste");
            else
            {
                var user = await db.Users.SingleAsync(x => x.Email == "admin-a@example.test");
                // Alteração administrativa controlada, fora do módulo Academic.
                if (change == "role") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE institution_member SET role = 'Teacher' WHERE user_id = {user.Id}");
                else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE institution_member SET institution_id = {factory.InstitutionB} WHERE user_id = {user.Id}");
            }
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Route, body)).StatusCode);
    }
}
