using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Api.IntegrationTests;

public sealed class AcademicClassTests
{
    internal static async Task<int> Unit(HttpClient client, int course, string code, int? year = 10) => (await AcademicCourseTests.Read(await client.PostAsJsonAsync(AcademicCourseUnitTests.Route(course), new { ClientId = Guid.NewGuid(), Code = code, Name = code, StudyYear = year }), HttpStatusCode.Created)).GetProperty("id").GetInt32();
    internal static string Route(int course) => $"{AcademicCourseTests.Route}/{course}/classes";
    [Fact]
    public async Task Classes_require_authentication()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route(1))).StatusCode);
    }
    [Fact]
    public async Task Units_accept_year_and_preserve_it_when_update_omits_year()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = factory.CreateClient();
        await AcademicCourseTests.Login(client, "admin-a@example.test"); var course = await AcademicCourseUnitTests.Course(client);
        var unit = await AcademicCourseTests.Read(await client.PostAsJsonAsync(AcademicCourseUnitTests.Route(course), new { ClientId = Guid.NewGuid(), Code = "M10", Name = "Matemática", StudyYear = 10 }), HttpStatusCode.Created);
        var edited = await AcademicCourseTests.Read(await client.PutAsJsonAsync($"{AcademicCourseUnitTests.Route(course)}/{unit.GetProperty("id").GetInt32()}", new { RowVersion = 1, Code = "M10", Name = "Nova" }));
        Assert.Equal(10, edited.GetProperty("studyYear").GetInt32());
    }
    [Fact]
    public async Task Selection_keeps_history_reactivates_links_and_blocks_year_transfer_after_removal()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = factory.CreateClient();
        await AcademicCourseTests.Login(client,"admin-a@example.test"); var course = await AcademicCourseUnitTests.Course(client);
        var a = await Unit(client,course,"A"); var b = await Unit(client,course,"B"); var route = Route(course);
        var key = Guid.NewGuid(); var body = new { ClientId=key,Code=" 10a ",Name=" Turma ",StudyYear=10,AcademicYear="2026/2027",UnitIds=new[]{b,a} };
        var created = await AcademicCourseTests.Read(await client.PostAsJsonAsync(route,body),HttpStatusCode.Created); var id = created.GetProperty("id").GetInt32();
        await AcademicCourseTests.Read(await client.PutAsJsonAsync($"{route}/{id}",new { RowVersion=1,Code="10A",Name="Editada",UnitIds=new[]{a} }));
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"{AcademicCourseUnitTests.Route(course)}/{b}",new { RowVersion=1,Code="B",Name="Outro ano",StudyYear=11 })).StatusCode);
        await AcademicCourseTests.Read(await client.PutAsJsonAsync($"{AcademicCourseUnitTests.Route(course)}/{b}",new { RowVersion=1,Code="B",Name="Programa editável" }));
        await AcademicCourseTests.Read(await client.PutAsJsonAsync($"{route}/{id}",new { RowVersion=2,Code="10A",Name="Editada",UnitIds=new[]{a,b} }));
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{AcademicCourseUnitTests.Route(course)}/{b}/archive",new { RowVersion=2 }));
        var retained = await AcademicCourseTests.Read(await client.PutAsJsonAsync($"{route}/{id}",new { RowVersion=3,Code="10A",Name="Retida",UnitIds=new[]{a,b} }));
        Assert.False(retained.GetProperty("units")[1].GetProperty("isActive").GetBoolean());
        await AcademicCourseTests.Read(await client.PutAsJsonAsync($"{route}/{id}",new { RowVersion=4,Code="10A",Name="Removida",UnitIds=new[]{a} }));
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"{route}/{id}",new { RowVersion=5,Code="10A",Name="Vedada",UnitIds=new[]{a,b} })).StatusCode);
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/archive",new { RowVersion=5 }));
        await AcademicCourseTests.Read(await client.PutAsJsonAsync($"{AcademicCourseUnitTests.Route(course)}/{a}",new { RowVersion=1,Code="A",Name="Mesmo ano",StudyYear=10 }));
        var parent = await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}"));
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{AcademicCourseTests.Route}/{course}/archive",new { RowVersion=parent.GetProperty("rowVersion").GetInt64() }));
        var replay = await AcademicCourseTests.Read(await client.PostAsJsonAsync(route,new { ClientId=key,Code="10A",Name="Turma",StudyYear=10,AcademicYear="2026/2027",UnitIds=new[]{a,b} }));
        Assert.True(replay.GetProperty("replay").GetBoolean()); Assert.False(replay.GetProperty("isActive").GetBoolean()); Assert.Single(replay.GetProperty("units").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"{route}/{id}/restore",new { RowVersion=6 })).StatusCode);
        using var services=factory.Services.CreateScope(); var db=services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Equal(2,await db.ClassUnits.IgnoreQueryFilters().CountAsync()); Assert.Single(await db.ClassUnits.IgnoreQueryFilters().Where(x=>x.UnitId==b).ToListAsync());
        Assert.Equal(6,await db.AuditEntries.CountAsync(x=>x.Target=="academic.class:"+key.ToString("D")&&x.Result=="success"));
    }
    [Fact]
    public async Task Classes_enforce_roles_tenants_relationships_strict_json_filters_versions_and_reserved_codes()
    {
        await using var factory=new IdentityFactory(); await factory.SeedAsync(); using var client=factory.CreateClient(); await AcademicCourseTests.Login(client,"admin-a@example.test");
        var course=await AcademicCourseUnitTests.Course(client); var other=await AcademicCourseUnitTests.Course(client,"OTHER"); var a=await Unit(client,course,"A"); var eleven=await Unit(client,course,"B",11); var none=await Unit(client,course,"C",null); var foreign=await Unit(client,other,"A"); var route=Route(course);
        object Body(int[] ids,int year=10,string code="10A")=>new {ClientId=Guid.NewGuid(),Code=code,Name="Turma",StudyYear=year,AcademicYear="2026/2027",UnitIds=ids};
        foreach(var email in new[]{"teacher-a@example.test","student-a@example.test","platform@example.test"}) { await AcademicCourseTests.Login(client,email); Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(route)).StatusCode); Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(route,Body([a]))).StatusCode); }
        await AcademicCourseTests.Login(client,"admin-b@example.test"); Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(route)).StatusCode); Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(route,Body([a]))).StatusCode);
        await AcademicCourseTests.Login(client,"admin-a@example.test");
        foreach(var ids in new int[][]{[],[a,a],Enumerable.Repeat(a,101).ToArray(),[0]}) Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(route,Body(ids))).StatusCode);
        foreach(var ids in new int[][]{[eleven],[none]}) Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(route,Body(ids))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(route,Body([foreign]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(route,Body([int.MaxValue]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),Code="A",Name="Turma",StudyYear=10,AcademicYear="2026/2027",UnitIds=new[]{a},InstitutionId=factory.InstitutionA})).StatusCode);
        var created=await AcademicCourseTests.Read(await client.PostAsJsonAsync(route,Body([a])),HttpStatusCode.Created); var id=created.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync($"{route}/{id}",new {RowVersion=1,Code="A",Name="A",UnitIds=new[]{a},StudyYear=11})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"{Route(other)}/{id}")).StatusCode);
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/archive",new {RowVersion=1}));
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(route,Body([a]))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"{route}/{id}/restore",new {RowVersion=1})).StatusCode);
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/restore",new {RowVersion=2}));
        await AcademicCourseTests.Read(await client.PostAsJsonAsync(route,Body([eleven],11,"11A")),HttpStatusCode.Created);
        var page=await AcademicCourseTests.Read(await client.GetAsync(route+"?studyYear=10&academicYear=2026%2F2027&pageSize=1&search=turma")); Assert.Equal(1,page.GetProperty("total").GetInt32()); Assert.Equal(1,page.GetProperty("items")[0].GetProperty("unitCount").GetInt32());
        Assert.Equal(1,(await AcademicCourseTests.Read(await client.GetAsync(AcademicCourseUnitTests.Route(course)+"?studyYear=10"))).GetProperty("total").GetInt32());
        foreach(var query in new[]{"studyYear=0","academicYear=2026/2028","page=0","pageSize=101","status=all","page=2147483647&pageSize=100","search="+new string('S',151)}) Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(route+"?"+query)).StatusCode);
    }
    [Theory]
    [InlineData("create")] [InlineData("update")] [InlineData("archive")] [InlineData("restore")]
    public async Task Audit_failure_rolls_back_parent_class_links_and_audit(string operation)
    {
        await using var factory=new IdentityFactory(); await factory.SeedAsync(); using var client=factory.CreateClient(); await AcademicCourseTests.Login(client,"admin-a@example.test");
        var course=await AcademicCourseUnitTests.Course(client); var a=await Unit(client,course,"A"); var b=await Unit(client,course,"B"); var route=Route(course); var id=0; long version=1;
        if(operation!="create") { id=(await AcademicCourseTests.Read(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),Code="A",Name="Inicial",StudyYear=10,AcademicYear="2026/2027",UnitIds=new[]{a}}),HttpStatusCode.Created)).GetProperty("id").GetInt32(); if(operation=="restore") { await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/archive",new {RowVersion=1})); version=2; } }
        var parentBefore=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}"));
        using(var setup=factory.Services.CreateScope()) await setup.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_class_audit BEFORE INSERT ON audit_entry WHEN NEW.target LIKE 'academic.class:%' AND NEW.result = 'success' BEGIN SELECT RAISE(ABORT, 'audit unavailable'); END;");
        using var failed=operation switch {"create"=>await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),Code="A",Name="Inicial",StudyYear=10,AcademicYear="2026/2027",UnitIds=new[]{a}}),"update"=>await client.PutAsJsonAsync($"{route}/{id}",new {RowVersion=version,Code="A",Name="Perdida",UnitIds=new[]{b}}),_=>await client.PostAsJsonAsync($"{route}/{id}/{operation}",new {RowVersion=version})};
        Assert.Equal(HttpStatusCode.InternalServerError,failed.StatusCode); Assert.Equal(parentBefore.GetRawText(),(await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}"))).GetRawText());
        if(id!=0) {var current=await AcademicCourseTests.Read(await client.GetAsync($"{route}/{id}")); Assert.Equal(version,current.GetProperty("rowVersion").GetInt64()); Assert.Equal("Inicial",current.GetProperty("name").GetString()); Assert.Equal(a,current.GetProperty("units")[0].GetProperty("unitId").GetInt32());}
        using var verify=factory.Services.CreateScope(); var db=verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>(); Assert.Equal(id==0?0:1,await db.ClassUnits.IgnoreQueryFilters().CountAsync());
    }
}
