using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class MySqlAcademicTests
{
    [MySqlFact]
    public async Task Upgrade_eight_to_nine_preserves_data_and_enforces_academic_uniqueness_concurrency_and_atomic_audit()
    {
        await using var factory = new IdentityFactory(mySqlConnectionString: Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"),
            migrationTarget: "20261016141237_SupportAttachments", supportEnabled: true);
        await factory.SeedAsync(); using var client = factory.CreateClient(); await AcademicCourseTests.Login(client, "admin-a@example.test");
        // Um agregado pré-existente representativo, sem executar cenários antigos.
        await SupportAuthorizationTests.Ticket(client);
        using (var services = factory.Services.CreateScope())
        {
            var db = services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            Assert.Equal(8, (await db.Database.GetAppliedMigrationsAsync()).Count());
            var institutions = JsonSerializer.Serialize(await db.Institutions.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
            var users = JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
            var members = JsonSerializer.Serialize(await db.Members.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id).ToListAsync());
            var tickets = JsonSerializer.Serialize(await db.SupportTickets.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id).ToListAsync());
            var messages = JsonSerializer.Serialize(await db.SupportMessages.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id).ToListAsync());
            var audit = JsonSerializer.Serialize(await db.AuditEntries.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
            await db.Database.MigrateAsync();
            Assert.Equal("MySql.EntityFrameworkCore", db.Database.ProviderName); Assert.Equal(9, (await db.Database.GetAppliedMigrationsAsync()).Count()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(institutions, JsonSerializer.Serialize(await db.Institutions.AsNoTracking().OrderBy(x => x.Id).ToListAsync()));
            Assert.Equal(users, JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync()));
            Assert.Equal(members, JsonSerializer.Serialize(await db.Members.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id).ToListAsync()));
            Assert.Equal(tickets, JsonSerializer.Serialize(await db.SupportTickets.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id).ToListAsync()));
            Assert.Equal(messages, JsonSerializer.Serialize(await db.SupportMessages.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id).ToListAsync()));
            Assert.Equal(audit, JsonSerializer.Serialize(await db.AuditEntries.AsNoTracking().OrderBy(x => x.Id).ToListAsync()));
        }
        var route = AcademicCourseTests.Route; var key = Guid.NewGuid(); var initial = new { ClientId = key, Code = " ti ", Name = " Curso ", Description = " Texto " };
        var responses = await Task.WhenAll(Enumerable.Range(0,2).Select(_ => client.PostAsJsonAsync(route, initial)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        var course = await responses.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>(); foreach (var response in responses) response.Dispose();
        var id = course.GetProperty("id").GetInt32(); Assert.Equal(DateTimeKind.Utc, course.GetProperty("createdAt").GetDateTime().Kind);
        var changes = await Task.WhenAll(new[] { "Um", "Dois" }.Select(name => client.PutAsJsonAsync($"{route}/{id}", new { RowVersion = 1, Code = "TI", Name = name, Description = "Novo" })));
        Assert.Single(changes, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(changes, x => x.StatusCode == HttpStatusCode.Conflict); foreach (var response in changes) response.Dispose();
        var saved = await AcademicCourseTests.Read(await client.GetAsync($"{route}/{id}")); Assert.Equal(2, saved.GetProperty("rowVersion").GetInt64());
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/archive", new { RowVersion = 2 }));
        var replay = await AcademicCourseTests.Read(await client.PostAsJsonAsync(route, initial)); Assert.True(replay.GetProperty("replay").GetBoolean()); Assert.False(replay.GetProperty("isActive").GetBoolean());
        Assert.Equal(saved.GetProperty("name").GetString(), replay.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { ClientId = key, Code = "TI", Name = "Outro" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "ti", Name = "Duplicado arquivado" })).StatusCode);
        using var other = factory.CreateClient(); await AcademicCourseTests.Login(other, "admin-b@example.test");
        await AcademicCourseTests.Read(await other.PostAsJsonAsync(route, initial), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{route}/{id}")).StatusCode);
        var codeRace = await Task.WhenAll(Enumerable.Range(0,2).Select(_ => client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "RACE", Name = "Concorrente" })));
        Assert.Single(codeRace, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(codeRace, x => x.StatusCode == HttpStatusCode.Conflict); foreach (var response in codeRace) response.Dispose();
        using (var services = factory.Services.CreateScope())
        {
            var db = services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            var stored = await db.Courses.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
            Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind); Assert.Equal(DateTimeKind.Utc, stored.UpdatedAt!.Value.Kind); Assert.Equal(DateTimeKind.Utc, stored.DeletedAt!.Value.Kind);
            Assert.Equal(3, stored.RowVersion); Assert.Equal(64, stored.CreationFingerprint.Length);
            var audits = await db.AuditEntries.Where(x => x.Target == "academic.course:" + key.ToString("D") && x.InstitutionId == factory.InstitutionA && x.Result == "success").ToListAsync();
            Assert.Equal(3, audits.Count); Assert.All(audits, x => { Assert.Null(x.Before); Assert.Null(x.After); });
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_academic_audit BEFORE INSERT ON audit_entry FOR EACH ROW BEGIN IF NEW.action LIKE 'academic.%' AND NEW.result = 'success' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'academic audit unavailable'; END IF; END");
        }
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync($"{route}/{id}/restore", new { RowVersion = 3 })).StatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "ROLLBACK", Name = "Não guardar" })).StatusCode);
            using var verify = factory.Services.CreateScope(); var db = verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            var stored = await db.Courses.IgnoreQueryFilters().SingleAsync(x => x.Id == id); Assert.False(stored.IsActive); Assert.Equal(3, stored.RowVersion);
            Assert.False(await db.Courses.IgnoreQueryFilters().AnyAsync(x => x.Code == "ROLLBACK"));
        }
        finally
        {
            using var cleanup = factory.Services.CreateScope(); await cleanup.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlRawAsync("DROP TRIGGER reject_academic_audit");
        }
        var restored = await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/restore", new { RowVersion = 3 })); Assert.Equal(4, restored.GetProperty("rowVersion").GetInt64());
    }
}
