using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Api.IntegrationTests;

public sealed class AcademicEnrolmentTests
{
    internal static string Route(int course, int value) => $"{AcademicClassTests.Route(course)}/{value}/enrolments";
    internal static async Task<(int Course, int Class, int Student)> Setup(IdentityFactory factory, HttpClient client)
    {
        await AcademicCourseTests.Login(client,"admin-a@example.test");
        var course=await AcademicCourseUnitTests.Course(client); var unit=await AcademicClassTests.Unit(client,course,"A");
        var value=await Class(client,course,unit,"A");
        using var services=factory.Services.CreateScope();
        var student=await services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Users.Where(x=>x.Email=="student-a@example.test").Select(x=>x.Id).SingleAsync();
        return(course,value,student);
    }
    internal static async Task<int> Class(HttpClient client,int course,int unit,string code,string year="2026/2027") => (await AcademicCourseTests.Read(await client.PostAsJsonAsync(AcademicClassTests.Route(course),new {ClientId=Guid.NewGuid(),Code=code,Name=code,StudyYear=10,AcademicYear=year,UnitIds=new[]{unit}}),HttpStatusCode.Created)).GetProperty("id").GetInt32();
    [Fact]
    public async Task Enrolment_routes_expose_contextual_roster_and_picker()
    {
        await using var factory=new IdentityFactory();await factory.SeedAsync();using var client=factory.CreateClient();var ids=await Setup(factory,client);var route=Route(ids.Course,ids.Class);
        var roster=await AcademicCourseTests.Read(await client.GetAsync(route));Assert.Equal(0,roster.GetProperty("total").GetInt32());
        var picker=await AcademicCourseTests.Read(await client.GetAsync(route+"/eligible-students"));Assert.Equal(ids.Student,picker.GetProperty("items")[0].GetProperty("studentId").GetInt32());
    }
    [Fact]
    public async Task Lifecycle_slot_history_and_replay_survive_unavailable_student_and_archived_parents_without_writes()
    {
        await using var factory=new IdentityFactory();await factory.SeedAsync();using var client=factory.CreateClient();var ids=await Setup(factory,client);var route=Route(ids.Course,ids.Class);var key=Guid.NewGuid();var body=new {ClientId=key,StudentId=ids.Student};
        var created=await AcademicCourseTests.Read(await client.PostAsJsonAsync(route,body),HttpStatusCode.Created);var id=created.GetProperty("id").GetInt32();Assert.Equal(10,created.GetProperty("studyYear").GetInt32());Assert.Equal("2026/2027",created.GetProperty("academicYear").GetString());
        Assert.Equal(0,(await AcademicCourseTests.Read(await client.GetAsync(route+"/eligible-students"))).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),StudentId=ids.Student})).StatusCode);
        var cancelled=await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/cancel",new {RowVersion=1}));Assert.False(cancelled.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"{route}/{id}/restore",new {RowVersion=1})).StatusCode);
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/restore",new {RowVersion=2}));
        var parent=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{ids.Class}"));
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{AcademicClassTests.Route(ids.Course)}/{ids.Class}/archive",new {RowVersion=parent.GetProperty("rowVersion").GetInt64()}));
        var course=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{ids.Course}"));
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{AcademicCourseTests.Route}/{ids.Course}/archive",new {RowVersion=course.GetProperty("rowVersion").GetInt64()}));
        using(var s=factory.Services.CreateScope())await s.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlInterpolatedAsync($"UPDATE user_account SET lockout_end={DateTimeOffset.UtcNow.AddHours(1)} WHERE id={ids.Student}");
        var courseBefore=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{ids.Course}"));
        var final=await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/cancel",new {RowVersion=3}));Assert.False(final.GetProperty("student").GetProperty("available").GetBoolean());
        Assert.Equal(courseBefore.GetRawText(),(await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{ids.Course}"))).GetRawText());
        var classBefore=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{ids.Class}"));
        var replay=await AcademicCourseTests.Read(await client.PostAsJsonAsync(route,body));Assert.True(replay.GetProperty("replay").GetBoolean());Assert.Equal(4,replay.GetProperty("rowVersion").GetInt64());
        Assert.Equal(classBefore.GetRawText(),(await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{ids.Class}"))).GetRawText());
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"{route}/{id}/restore",new {RowVersion=4})).StatusCode);
        Assert.Equal(1,(await AcademicCourseTests.Read(await client.GetAsync(route+"?status=Cancelled&search=STUDENT-A"))).GetProperty("total").GetInt32());
        using var verify=factory.Services.CreateScope();Assert.Equal(4,await verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().AuditEntries.CountAsync(x=>x.Target=="academic.enrolment:"+key.ToString("D")&&x.Result=="success"));
    }
    [Fact]
    public async Task Routes_enforce_roles_tenants_relations_strict_bodies_versions_and_validation()
    {
        await using var factory=new IdentityFactory();await factory.SeedAsync();using var client=factory.CreateClient();var ids=await Setup(factory,client);var route=Route(ids.Course,ids.Class);
        using(var anonymous=factory.CreateClient())Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(route)).StatusCode);
        foreach(var email in new[]{"teacher-a@example.test","student-a@example.test","platform@example.test"}){await AcademicCourseTests.Login(client,email);Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(route)).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),StudentId=ids.Student})).StatusCode);}
        await AcademicCourseTests.Login(client,"admin-b@example.test");Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(route)).StatusCode);
        await AcademicCourseTests.Login(client,"admin-a@example.test");
        foreach(var query in new[]{"page=0","pageSize=101","status=Archived","page=2147483647&pageSize=100","search="+new string('a',151)})Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(route+"?"+query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),StudentId=ids.Student,StudyYear=10})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),StudentId=int.MaxValue})).StatusCode);
        using var s=factory.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();foreach(var email in new[]{"teacher-a@example.test","student-b@example.test"}){var student=await db.Users.Where(x=>x.Email==email).Select(x=>x.Id).SingleAsync();Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),StudentId=student})).StatusCode);}
        var created=await AcademicCourseTests.Read(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),StudentId=ids.Student}),HttpStatusCode.Created);var id=created.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(route,new {ClientId=created.GetProperty("clientId").GetGuid(),StudentId=ids.Student+1})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(Route(ids.Course,ids.Class+1)+$"/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"{route}/{id}/cancel",new {RowVersion=0})).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,(await client.PutAsJsonAsync($"{route}/{id}",new {})).StatusCode);
    }
    [Fact]
    public async Task Picker_filters_availability_search_and_cross_class_slots_before_totals_and_paging_and_keeps_cancelled_history()
    {
        await using var factory=new IdentityFactory();await factory.SeedAsync();using var client=factory.CreateClient();var ids=await Setup(factory,client);var route=Route(ids.Course,ids.Class);
        var names=new[]{"Álvaro bloqueado","Bárbara eliminada","Carolina ativa","Diana ativa","Érica desativada","Fátima removida"};var students=new List<int>();
        foreach(var name in names)
        {
            var user=await AcademicCourseTests.Read(await client.PostAsJsonAsync("/api/v1/identity/users",new {Email=$"candidate{students.Count}@example.test",Password=IdentityFactory.Password,Name=name,Role="Student"}),HttpStatusCode.Created);students.Add(user.GetProperty("id").GetInt32());
        }
        using(var services=factory.Services.CreateScope())
        {
            var db=services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE user_account SET lockout_end={DateTimeOffset.UtcNow.AddHours(1)} WHERE id={students[0]}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE user_account SET deleted_at={DateTime.UtcNow} WHERE id={students[1]}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE user_account SET is_active={false} WHERE id={students[4]}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE institution_member SET deleted_at={DateTime.UtcNow} WHERE user_id={students[5]}");
        }
        var unit=await AcademicClassTests.Unit(client,ids.Course,"B");var second=await Class(client,ids.Course,unit,"B");var nextYear=await Class(client,ids.Course,unit,"NEXT","2027/2028");
        var otherRoute=Route(ids.Course,second);
        var occupied=await AcademicCourseTests.Read(await client.PostAsJsonAsync(otherRoute,new {ClientId=Guid.NewGuid(),StudentId=students[2]}),HttpStatusCode.Created);
        var page=await AcademicCourseTests.Read(await client.GetAsync(route+"/eligible-students?pageSize=1"));Assert.Equal(2,page.GetProperty("total").GetInt32());Assert.Equal(students[3],page.GetProperty("items")[0].GetProperty("studentId").GetInt32());
        Assert.Equal(0,(await AcademicCourseTests.Read(await client.GetAsync(route+"/eligible-students?pageSize=1&search=BÁRBARA"))).GetProperty("total").GetInt32());
        Assert.Equal(1,(await AcademicCourseTests.Read(await client.GetAsync(route+"/eligible-students?pageSize=1&search=DIANA"))).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),StudentId=students[2]})).StatusCode);
        await AcademicCourseTests.Read(await client.PostAsJsonAsync(Route(ids.Course,nextYear),new {ClientId=Guid.NewGuid(),StudentId=students[2]}),HttpStatusCode.Created);
        var id=occupied.GetProperty("id").GetInt32();await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{otherRoute}/{id}/cancel",new {RowVersion=1}));
        Assert.Equal(3,(await AcademicCourseTests.Read(await client.GetAsync(route+"/eligible-students?pageSize=1"))).GetProperty("total").GetInt32());
        Assert.Equal(2,(await AcademicCourseTests.Read(await client.GetAsync(otherRoute+"/eligible-students?pageSize=1"))).GetProperty("total").GetInt32());
        var own=await AcademicCourseTests.Read(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),StudentId=students[2]}),HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"{otherRoute}/{id}/restore",new {RowVersion=2})).StatusCode);
        foreach(var student in new[]{students[0],students[1],students[4],students[5]})Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),StudentId=student})).StatusCode);
        var filtered=await AcademicCourseTests.Read(await client.GetAsync(route+"?search=CAROLINA&pageSize=1"));Assert.Equal(1,filtered.GetProperty("total").GetInt32());Assert.Equal("Carolina ativa",filtered.GetProperty("items")[0].GetProperty("student").GetProperty("name").GetString());
    }
    [Theory]
    [InlineData("create")][InlineData("cancel")][InlineData("restore")]
    public async Task Audit_failure_rolls_back_enrolment_slot_and_both_parent_versions(string operation)
    {
        await using var factory=new IdentityFactory();await factory.SeedAsync();using var client=factory.CreateClient();var ids=await Setup(factory,client);var route=Route(ids.Course,ids.Class);var key=Guid.NewGuid();var id=0;long version=1;
        if(operation!="create"){id=(await AcademicCourseTests.Read(await client.PostAsJsonAsync(route,new {ClientId=key,StudentId=ids.Student}),HttpStatusCode.Created)).GetProperty("id").GetInt32();if(operation=="restore"){await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{route}/{id}/cancel",new {RowVersion=1}));version=2;}}
        var courseBefore=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{ids.Course}"));var classBefore=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{ids.Class}"));
        using(var s=factory.Services.CreateScope())await s.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_enrolment_audit BEFORE INSERT ON audit_entry WHEN NEW.target LIKE 'academic.enrolment:%' AND NEW.result='success' BEGIN SELECT RAISE(ABORT,'audit unavailable'); END;");
        using var failed=operation=="create"?await client.PostAsJsonAsync(route,new {ClientId=key,StudentId=ids.Student}):await client.PostAsJsonAsync($"{route}/{id}/{operation}",new {RowVersion=version});Assert.Equal(HttpStatusCode.InternalServerError,failed.StatusCode);
        Assert.Equal(courseBefore.GetRawText(),(await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{ids.Course}"))).GetRawText());Assert.Equal(classBefore.GetRawText(),(await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{ids.Class}"))).GetRawText());
        using var verify=factory.Services.CreateScope();var db=verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();var value=await db.Enrolments.IgnoreQueryFilters().SingleOrDefaultAsync();if(operation=="create")Assert.Null(value);else{Assert.Equal(version,value!.RowVersion);Assert.Equal(operation=="cancel",value.IsActive);Assert.Equal(value.IsActive?ids.Student:null,value.ActiveStudentId);}
    }}
