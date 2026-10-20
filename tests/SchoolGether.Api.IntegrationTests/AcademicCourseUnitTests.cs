using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class AcademicCourseUnitTests
{
    internal static string Route(int course) => $"{AcademicCourseTests.Route}/{course}/units";
    internal static async Task<int> Course(HttpClient client, string code = "TI") => (await AcademicCourseTests.Read(await client.PostAsJsonAsync(AcademicCourseTests.Route, new { ClientId = Guid.NewGuid(), Code = code, Name = "Curso " + code }), HttpStatusCode.Created)).GetProperty("id").GetInt32();
    [Fact]
    public async Task Unit_routes_require_authentication_and_create_with_own_programme()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route(1))).StatusCode);
        await AcademicCourseTests.Login(client, "admin-a@example.test"); var course = await Course(client);
        var created = await AcademicCourseTests.Read(await client.PostAsJsonAsync(Route(course), new { ClientId = Guid.NewGuid(), Code = " mat ", Name = " Matemática ", Syllabus = " Programa próprio " }), HttpStatusCode.Created);
        Assert.Equal(course, created.GetProperty("courseId").GetInt32()); Assert.Equal("MAT", created.GetProperty("code").GetString()); Assert.Equal("Programa próprio", created.GetProperty("syllabus").GetString());
        Assert.Equal(2, (await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}"))).GetProperty("rowVersion").GetInt64());
    }
    [Fact]
    public async Task Lifecycle_replay_binds_initial_parent_author_and_payload_even_after_parent_archive()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = factory.CreateClient(); await AcademicCourseTests.Login(client, "admin-a@example.test");
        var course = await Course(client); var second = await Course(client, "OTHER"); var route = Route(course);
        var key = Guid.NewGuid(); var body = new { ClientId = key, Code = " a_1 ", Name = " Disciplina ", Syllabus = " Inicial " };
        var unit = await AcademicCourseTests.Read(await client.PostAsJsonAsync(route, body), HttpStatusCode.Created); var id = unit.GetProperty("id").GetInt32();
        var edited = await AcademicCourseTests.Read(await client.PutAsJsonAsync($"{route}/{id}", new { RowVersion = 1, Code = "B", Name = "Alterada", Syllabus = "Novo" })); Assert.Equal(2, edited.GetProperty("rowVersion").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"{route}/{id}", new { RowVersion = 1, Code = "B", Name = "Perdida" })).StatusCode);
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/archive", new { RowVersion = 2 }));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "b", Name = "Ocupada" })).StatusCode);
        await AcademicCourseTests.Read(await client.PostAsJsonAsync(Route(second), new { ClientId = Guid.NewGuid(), Code = "b", Name = "Outro curso" }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route(second)}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route(second), body)).StatusCode);
        var parent = await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}"));
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{AcademicCourseTests.Route}/{course}/archive", new { RowVersion = parent.GetProperty("rowVersion").GetInt64() }));
        var replay = await AcademicCourseTests.Read(await client.PostAsJsonAsync(route, body)); Assert.True(replay.GetProperty("replay").GetBoolean()); Assert.Equal("Novo", replay.GetProperty("syllabus").GetString()); Assert.False(replay.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{route}/{id}/restore", new { RowVersion = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "NEW", Name = "Vedada" })).StatusCode);
        Assert.Equal(1, (await AcademicCourseTests.Read(await client.GetAsync(route + "?status=Archived&search=alterada"))).GetProperty("total").GetInt32());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Equal(3, await db.AuditEntries.CountAsync(x => x.Target == "academic.unit:" + key.ToString("D") && x.Result == "success"));
    }
    [Fact]
    public async Task Units_enforce_roles_tenants_strict_json_validation_and_pagination()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = factory.CreateClient(); await AcademicCourseTests.Login(client, "admin-a@example.test"); var course = await Course(client); var route = Route(course);
        foreach (var email in new[] { "teacher-a@example.test", "student-a@example.test", "platform@example.test" }) { await AcademicCourseTests.Login(client, email); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode); }
        await AcademicCourseTests.Login(client, "admin-b@example.test"); Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "A", Name = "A" })).StatusCode);
        await AcademicCourseTests.Login(client, "admin-a@example.test");
        foreach (var extra in new[] { "courseId", "institutionId", "createdBy" }) { using var response = await client.PostAsJsonAsync(route, new Dictionary<string, object> { ["clientId"] = Guid.NewGuid(), ["code"] = "A", ["name"] = "A", [extra] = course }); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        await AcademicCourseTests.Read(await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = " " + new string('a', 30) + " ", Name = " " + new string('N', 200) + " ", Syllabus = " " + new string('S', 5000) + " " }), HttpStatusCode.Created);
        for (var i = 0; i < 3; i++) await AcademicCourseTests.Read(await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "U" + i, Name = "Disciplina " + i }), HttpStatusCode.Created);
        var page = await AcademicCourseTests.Read(await client.GetAsync(route + "?page=2&pageSize=2")); Assert.Equal(4, page.GetProperty("total").GetInt32()); Assert.Equal(2, page.GetProperty("items").GetArrayLength());
        Assert.Equal(3, (await AcademicCourseTests.Read(await client.GetAsync(route + "?search=disciplina"))).GetProperty("total").GetInt32());
        foreach (var query in new[] { "page=0", "pageSize=101", "status=all", "page=2147483647&pageSize=100", "search=" + new string('S', 151) }) Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(route + "?" + query)).StatusCode);
        foreach (var code in new[] { "Á", "_A", "A B", "" }) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = code, Name = "Unidade" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "LONG", Name = "Unidade", Syllabus = new string('S', 5001) })).StatusCode);
    }
    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("archive")]
    [InlineData("restore")]
    public async Task Audit_failure_rolls_back_parent_unit_and_audit(string operation)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = factory.CreateClient(); await AcademicCourseTests.Login(client, "admin-a@example.test"); var course = await Course(client); var route = Route(course); var id = 0; long version = 1;
        if (operation != "create") { id = (await AcademicCourseTests.Read(await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "A", Name = "Inicial" }), HttpStatusCode.Created)).GetProperty("id").GetInt32(); if (operation == "restore") { await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/archive", new { RowVersion = 1 })); version = 2; } }
        var before = await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}"));
        using (var setup = factory.Services.CreateScope()) await setup.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_unit_audit BEFORE INSERT ON audit_entry WHEN NEW.target LIKE 'academic.unit:%' AND NEW.result = 'success' BEGIN SELECT RAISE(ABORT, 'audit unavailable'); END;");
        using var failed = operation switch { "create" => await client.PostAsJsonAsync(route, new { ClientId = Guid.NewGuid(), Code = "A", Name = "Inicial" }), "update" => await client.PutAsJsonAsync($"{route}/{id}", new { RowVersion = version, Code = "A", Name = "Perdida" }), _ => await client.PostAsJsonAsync($"{route}/{id}/{operation}", new { RowVersion = version }) }; Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        var after = await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}")); Assert.Equal(before.GetRawText(), after.GetRawText());
        if (id != 0) { var unit = await AcademicCourseTests.Read(await client.GetAsync($"{route}/{id}")); Assert.Equal(version, unit.GetProperty("rowVersion").GetInt64()); Assert.Equal("Inicial", unit.GetProperty("name").GetString()); } else Assert.Equal(0, (await AcademicCourseTests.Read(await client.GetAsync(route))).GetProperty("total").GetInt32());
    }
}
