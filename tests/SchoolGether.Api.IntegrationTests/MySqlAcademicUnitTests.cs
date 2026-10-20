using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Api.IntegrationTests;

public sealed class MySqlAcademicUnitTests
{
    [MySqlFact]
    public async Task Upgrade_nine_to_ten_preserves_catalogue_and_enforces_units_races_fk_utc_and_atomic_audit()
    {
        await using var factory = new IdentityFactory(mySqlConnectionString: Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"), migrationTarget: "20261020083752_AcademicCourses");
        await factory.SeedAsync(); using var client = factory.CreateClient(); await AcademicCourseTests.Login(client, "admin-a@example.test"); var course = await AcademicCourseUnitTests.Course(client); var second = await AcademicCourseUnitTests.Course(client, "SECOND");
        using (var services = factory.Services.CreateScope())
        {
            var db = services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>(); Assert.Equal(9, (await db.Database.GetAppliedMigrationsAsync()).Count());
            var courses = JsonSerializer.Serialize(await db.Courses.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id).ToListAsync()); var institutions = JsonSerializer.Serialize(await db.Institutions.AsNoTracking().OrderBy(x => x.Id).ToListAsync()); var users = JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync()); var members = JsonSerializer.Serialize(await db.Members.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id).ToListAsync()); var audits = JsonSerializer.Serialize(await db.AuditEntries.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
            await db.Database.MigrateAsync(); Assert.Equal("MySql.EntityFrameworkCore", db.Database.ProviderName); Assert.Equal(10, (await db.Database.GetAppliedMigrationsAsync()).Count()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(courses, JsonSerializer.Serialize(await db.Courses.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id).ToListAsync())); Assert.Equal(institutions, JsonSerializer.Serialize(await db.Institutions.AsNoTracking().OrderBy(x => x.Id).ToListAsync())); Assert.Equal(users, JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync())); Assert.Equal(members, JsonSerializer.Serialize(await db.Members.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id).ToListAsync())); Assert.Equal(audits, JsonSerializer.Serialize(await db.AuditEntries.AsNoTracking().OrderBy(x => x.Id).ToListAsync()));
        }
        var route = AcademicCourseUnitTests.Route(course); var key = Guid.NewGuid(); var initial = new { ClientId = key, Code = " u ", Name = " Unidade ", Syllabus = " Programa " };
        var creates = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync(route, initial))); Assert.Single(creates, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(creates, x => x.StatusCode == HttpStatusCode.OK); var unit = await creates.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>(); foreach (var r in creates) r.Dispose(); var id = unit.GetProperty("id").GetInt32(); Assert.Equal(DateTimeKind.Utc, unit.GetProperty("createdAt").GetDateTime().Kind);
        var edits = await Task.WhenAll(new[] { "Primeira", "Segunda" }.Select(name => client.PutAsJsonAsync($"{route}/{id}", new { RowVersion = 1, Code = "U", Name = name, Syllabus = "Alterado" }))); Assert.Single(edits, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(edits, x => x.StatusCode == HttpStatusCode.Conflict); foreach (var r in edits) r.Dispose();
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/archive", new { RowVersion = 2 }));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "u", Name = "Código reservado" })).StatusCode);
        await AcademicCourseTests.Read(await client.PostAsJsonAsync(AcademicCourseUnitTests.Route(second), new { ClientId = Guid.NewGuid(), Code = "u", Name = "Outro curso" }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(AcademicCourseUnitTests.Route(second), initial)).StatusCode);
        using (var services = factory.Services.CreateScope())
        {
            var db = services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>(); var stored = await db.CourseUnits.IgnoreQueryFilters().SingleAsync(x => x.Id == id); Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind); Assert.Equal(DateTimeKind.Utc, stored.UpdatedAt!.Value.Kind); Assert.Equal(DateTimeKind.Utc, stored.DeletedAt!.Value.Kind);
            // SQL direto só no teste: comprova que a FK composta também resiste fora das guardas EF.
            var fk = await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic_course_unit SET institution_id={factory.InstitutionB} WHERE id={id}")); Assert.Contains(fk.Number, new[] { 1216, 1452 });
        }
        var codeRace = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "RACE", Name = "Corrida" }))); Assert.Single(codeRace, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(codeRace, x => x.StatusCode == HttpStatusCode.Conflict); foreach (var r in codeRace) r.Dispose();
        var parent = await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}")); await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{AcademicCourseTests.Route}/{course}/archive", new { RowVersion = parent.GetProperty("rowVersion").GetInt64() }));
        var replay = await AcademicCourseTests.Read(await client.PostAsJsonAsync(route, initial)); Assert.True(replay.GetProperty("replay").GetBoolean()); Assert.Equal("Alterado", replay.GetProperty("syllabus").GetString()); Assert.False(replay.GetProperty("isActive").GetBoolean());
        parent = await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}")); await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{AcademicCourseTests.Route}/{course}/restore", new { RowVersion = parent.GetProperty("rowVersion").GetInt64() }));
        // Os dois pedidos usam a mesma versão do pai: exatamente um pode confirmar.
        var raceUnit = await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/restore", new { RowVersion = 3 })); parent = await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}"));
        var parentRace = await Task.WhenAll(client.PostAsJsonAsync($"{AcademicCourseTests.Route}/{course}/archive", new { RowVersion = parent.GetProperty("rowVersion").GetInt64() }), client.PutAsJsonAsync($"{route}/{id}", new { RowVersion = 4, Code = "U", Name = "Corrida pai", Syllabus = "Corrida" })); Assert.Single(parentRace, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(parentRace, x => x.StatusCode == HttpStatusCode.Conflict); var archiveWon = parentRace[0].StatusCode == HttpStatusCode.OK; foreach (var r in parentRace) r.Dispose();
        var afterRace = await AcademicCourseTests.Read(await client.GetAsync($"{route}/{id}")); Assert.Equal(archiveWon ? 4 : 5, afterRace.GetProperty("rowVersion").GetInt64());
        if (archiveWon) { parent = await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}")); await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{AcademicCourseTests.Route}/{course}/restore", new { RowVersion = parent.GetProperty("rowVersion").GetInt64() })); }
        var beforeParent = await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}"));
        using (var services = factory.Services.CreateScope()) await services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_unit_audit BEFORE INSERT ON audit_entry FOR EACH ROW BEGIN IF NEW.target LIKE 'academic.unit:%' AND NEW.result='success' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='unit audit unavailable'; END IF; END");
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PutAsJsonAsync($"{route}/{id}", new { RowVersion = afterRace.GetProperty("rowVersion").GetInt64(), Code = "ROLLBACK", Name = "Perdida" })).StatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "ROLLBACK", Name = "Perdida" })).StatusCode);
            Assert.Equal(beforeParent.GetRawText(), (await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}"))).GetRawText()); Assert.Equal(afterRace.GetRawText(), (await AcademicCourseTests.Read(await client.GetAsync($"{route}/{id}"))).GetRawText());
            using var verify = factory.Services.CreateScope(); var db = verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>(); Assert.False(await db.CourseUnits.IgnoreQueryFilters().AnyAsync(x => x.Code == "ROLLBACK")); Assert.All(await db.AuditEntries.Where(x => x.Target == "academic.unit:" + key.ToString("D") && x.Result == "success").ToListAsync(), a => { Assert.Null(a.Before); Assert.Null(a.After); });
        }
        finally { using var cleanup = factory.Services.CreateScope(); await cleanup.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlRawAsync("DROP TRIGGER reject_unit_audit"); }
    }
}
