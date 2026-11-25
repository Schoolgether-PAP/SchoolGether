using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using SchoolGether.Domain.Academic;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Api.IntegrationTests;

public sealed class MySqlAcademicEnrolmentTests
{
    private sealed class SaveBarrier:SaveChangesInterceptor
    {
        private int mode,arrivals;
        private TaskCompletionSource released=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentBag<long> CourseVersions{get;}=[];
        public ConcurrentBag<long> ClassVersions{get;}=[];
        public void Arm(int value){mode=value;arrivals=0;CourseVersions.Clear();ClassVersions.Clear();released=new(TaskCreationOptions.RunContinuationsAsynchronously);}
        public void Disarm()=>mode=0;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,InterceptionResult<int> result,CancellationToken ct=default)
        {
            var db=eventData.Context!;
            if(mode==0||!db.ChangeTracker.Entries<AcademicEnrolment>().Any(x=>x.State==EntityState.Added)&&!(mode==3&&db.ChangeTracker.Entries<AcademicClass>().Any(x=>x.Property(c=>c.IsActive).IsModified)))return result;
            CourseVersions.Add(db.ChangeTracker.Entries<Course>().Single(x=>x.State==EntityState.Modified).Property(x=>x.RowVersion).OriginalValue);
            ClassVersions.Add(db.ChangeTracker.Entries<AcademicClass>().Single(x=>x.State==EntityState.Modified).Property(x=>x.RowVersion).OriginalValue);
            if(Interlocked.Increment(ref arrivals)==2)released.TrySetResult();
            await released.Task.WaitAsync(TimeSpan.FromSeconds(20),ct);return result;
        }
    }
    [MySqlFact]
    public async Task Upgrade_published_schema_preserves_history_enforces_fk_check_slots_and_serializes_races_with_atomic_audit()
    {
        var barrier=new SaveBarrier();
        await using var factory=new IdentityFactory(mySqlConnectionString:Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"),migrationTarget:"20261109222457_AnnualTeacherPlans");
        using var instrumented=factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>s.ConfigureDbContext<SchoolGetherDbContext>(o=>o.AddInterceptors(barrier))));
        await factory.SeedAsync();using var client=instrumented.CreateClient();var ids=await AcademicEnrolmentTests.Setup(factory,client);
        using(var services=factory.Services.CreateScope())
        {
            var db=services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();Assert.Equal(12,(await db.Database.GetAppliedMigrationsAsync()).Count());
            var before=await Legacy(db);await db.Database.MigrateAsync();Assert.Contains("20261125093431_AcademicMemberships",await db.Database.GetAppliedMigrationsAsync());Assert.Empty(await db.Database.GetPendingMigrationsAsync());Assert.Equal("MySql.EntityFrameworkCore",db.Database.ProviderName);Assert.Equal(before,await Legacy(db));
        }
        var unit=await AcademicClassTests.Unit(client,ids.Course,"B");var second=await AcademicEnrolmentTests.Class(client,ids.Course,unit,"B");var nextYear=await AcademicEnrolmentTests.Class(client,ids.Course,unit,"NEXT","2027/2028");
        var route=AcademicEnrolmentTests.Route(ids.Course,ids.Class);var secondRoute=AcademicEnrolmentTests.Route(ids.Course,second);var nextRoute=AcademicEnrolmentTests.Route(ids.Course,nextYear);
        var parentBefore=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{ids.Course}"));
        barrier.Arm(1);var competing=await Task.WhenAll(client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),StudentId=ids.Student}),client.PostAsJsonAsync(secondRoute,new {ClientId=Guid.NewGuid(),StudentId=ids.Student}));barrier.Disarm();
        AssertBarrier(barrier);Assert.Single(competing,x=>x.StatusCode==HttpStatusCode.Created);Assert.Single(competing,x=>x.StatusCode==HttpStatusCode.Conflict);
        var winner=await competing.Single(x=>x.StatusCode==HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>();foreach(var response in competing)response.Dispose();var winnerClass=winner.GetProperty("classId").GetInt32();var winnerId=winner.GetProperty("id").GetInt32();var winnerRoute=AcademicEnrolmentTests.Route(ids.Course,winnerClass);
        var loserClass=winnerClass==ids.Class?second:ids.Class;var loserRoute=AcademicEnrolmentTests.Route(ids.Course,loserClass);
        Assert.Equal(parentBefore.GetProperty("rowVersion").GetInt64()+1,(await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{ids.Course}"))).GetProperty("rowVersion").GetInt64());
        Assert.Equal(1,(await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{loserClass}"))).GetProperty("rowVersion").GetInt64());
        Assert.Equal(DateTimeKind.Utc,winner.GetProperty("createdAt").GetDateTime().Kind);
        using(var services=factory.Services.CreateScope())
        {
            var db=services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();Assert.Single(await db.Enrolments.IgnoreQueryFilters().ToArrayAsync());
            await CheckSql(db,$"UPDATE academic_enrolment SET active_student_id=NULL WHERE id={winnerId}",3819);
            await CheckSql(db,$"UPDATE academic_enrolment SET study_year=11 WHERE id={winnerId}",1452);
            var foreign=await db.Users.Where(x=>x.Email=="student-b@example.test").Select(x=>x.Id).SingleAsync();
            await CheckSql(db,$"UPDATE academic_enrolment SET student_id={foreign},active_student_id={foreign} WHERE id={winnerId}",1452);
            await CheckSql(db,$"INSERT INTO academic_enrolment (institution_id,course_id,class_id,student_id,active_student_id,study_year,academic_year,client_id,is_active,row_version,created_at,created_by,creation_fingerprint) SELECT institution_id,course_id,{loserClass},student_id,active_student_id,study_year,academic_year,'{Guid.NewGuid():D}',is_active,row_version,created_at,created_by,creation_fingerprint FROM academic_enrolment WHERE id={winnerId}",1062);
        }
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{winnerRoute}/{winnerId}/cancel",new {RowVersion=1}));
        var other=await AcademicCourseTests.Read(await client.PostAsJsonAsync(loserRoute,new {ClientId=Guid.NewGuid(),StudentId=ids.Student}),HttpStatusCode.Created);var otherId=other.GetProperty("id").GetInt32();
        await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{loserRoute}/{otherId}/cancel",new {RowVersion=1}));
        using(var services=factory.Services.CreateScope())
        {
            var db=services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();Assert.Equal(2,await db.Enrolments.IgnoreQueryFilters().CountAsync(x=>!x.IsActive&&x.ActiveStudentId==null));
            await CheckSql(db,$"UPDATE academic_enrolment SET active_student_id=student_id WHERE id={winnerId}",3819);
            await CheckSql(db,$"INSERT INTO academic_enrolment (institution_id,course_id,class_id,student_id,active_student_id,study_year,academic_year,client_id,is_active,row_version,created_at,created_by,creation_fingerprint) SELECT institution_id,course_id,class_id,student_id,NULL,study_year,academic_year,'{Guid.NewGuid():D}',is_active,row_version,created_at,created_by,creation_fingerprint FROM academic_enrolment WHERE id={winnerId}",1062);
        }
        var replayKey=Guid.NewGuid();var body=new {ClientId=replayKey,StudentId=ids.Student};
        barrier.Arm(2);var replays=await Task.WhenAll(client.PostAsJsonAsync(nextRoute,body),client.PostAsJsonAsync(nextRoute,body));barrier.Disarm();AssertBarrier(barrier);Assert.Single(replays,x=>x.StatusCode==HttpStatusCode.Created);Assert.Single(replays,x=>x.StatusCode==HttpStatusCode.OK);
        var replayCreated=await replays.Single(x=>x.StatusCode==HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>();var replayId=replayCreated.GetProperty("id").GetInt32();foreach(var response in replays)response.Dispose();
        using(var services=factory.Services.CreateScope())Assert.Equal(1,await services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().AuditEntries.CountAsync(x=>x.Target=="academic.enrolment:"+replayKey.ToString("D")&&x.Result=="success"));
        var fresh=await AcademicCourseTests.Read(await client.PostAsJsonAsync("/api/v1/identity/users",new {Email="race-student@example.test",Password=IdentityFactory.Password,Name="Aluno concorrente",Role="Student"}),HttpStatusCode.Created);var student2=fresh.GetProperty("id").GetInt32();
        var groupBefore=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{nextYear}"));
        barrier.Arm(3);var archiveRace=await Task.WhenAll(client.PostAsJsonAsync(nextRoute,new {ClientId=Guid.NewGuid(),StudentId=student2}),client.PostAsJsonAsync($"{AcademicClassTests.Route(ids.Course)}/{nextYear}/archive",new {RowVersion=groupBefore.GetProperty("rowVersion").GetInt64()}));barrier.Disarm();AssertBarrier(barrier);Assert.Single(archiveRace,x=>x.IsSuccessStatusCode);Assert.Single(archiveRace,x=>x.StatusCode==HttpStatusCode.Conflict);foreach(var response in archiveRace)response.Dispose();
        var group=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{nextYear}"));
        using(var services=factory.Services.CreateScope())Assert.Equal(group.GetProperty("isActive").GetBoolean(),await services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Enrolments.IgnoreQueryFilters().AnyAsync(x=>x.ClassId==nextYear&&x.StudentId==student2));
        // Trigger real do provider: cada operação deve reverter lugar, versões e auditoria em conjunto.
        foreach(var operation in new[]{"create","cancel","restore"})
        {
            if(operation=="restore")await AcademicCourseTests.Read(await client.PostAsJsonAsync($"{nextRoute}/{replayId}/cancel",new {RowVersion=1}));
            var beforeCourse=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{ids.Course}"));var beforeClass=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{winnerClass}"));
            var beforeNext=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{nextYear}"));
            string rowsBefore;
            using(var services=factory.Services.CreateScope()){var db=services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();rowsBefore=JsonSerializer.Serialize(await db.Enrolments.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToListAsync());await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_enrolment_audit BEFORE INSERT ON audit_entry FOR EACH ROW BEGIN IF NEW.target LIKE 'academic.enrolment:%' AND NEW.result='success' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='enrolment audit unavailable'; END IF; END");}
            try
            {
                // Create usa a turma original ativa; cancel aceita a turma 2027 mesmo que arquivada.
                using var failed=operation=="create"?await client.PostAsJsonAsync(winnerRoute,new {ClientId=Guid.NewGuid(),StudentId=student2}):operation=="cancel"?await client.PostAsJsonAsync($"{nextRoute}/{replayId}/cancel",new {RowVersion=1}):await client.PostAsJsonAsync($"{winnerRoute}/{winnerId}/restore",new {RowVersion=2});
                Assert.Equal(HttpStatusCode.InternalServerError,failed.StatusCode);
                Assert.Equal(beforeCourse.GetRawText(),(await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{ids.Course}"))).GetRawText());Assert.Equal(beforeClass.GetRawText(),(await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{winnerClass}"))).GetRawText());Assert.Equal(beforeNext.GetRawText(),(await AcademicCourseTests.Read(await client.GetAsync($"{AcademicClassTests.Route(ids.Course)}/{nextYear}"))).GetRawText());
                using var services=factory.Services.CreateScope();var db=services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();Assert.Equal(rowsBefore,JsonSerializer.Serialize(await db.Enrolments.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToListAsync()));Assert.All(await db.AuditEntries.Where(x=>x.Target.StartsWith("academic.enrolment:")&&x.Result=="success").ToListAsync(),x=>{Assert.Null(x.Before);Assert.Null(x.After);});
            }
            finally{using var services=factory.Services.CreateScope();await services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlRawAsync("DROP TRIGGER reject_enrolment_audit");}
        }
    }
    private static void AssertBarrier(SaveBarrier barrier){Assert.Equal(2,barrier.CourseVersions.Count);Assert.Single(barrier.CourseVersions.Distinct());Assert.Equal(2,barrier.ClassVersions.Count);Assert.Single(barrier.ClassVersions.Distinct());}
    private static async Task CheckSql(SchoolGetherDbContext db,string sql,int expected)
    {
#pragma warning disable EF1002 // SQL apenas de teste, números/chaves gerados pelo próprio teste; sem input externo.
        var error=await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlRawAsync(sql));
#pragma warning restore EF1002
        if(expected==1452)Assert.Contains(error.Number,new[]{1216,1452});else Assert.Equal(expected,error.Number);
    }
    private static async Task<string> Legacy(SchoolGetherDbContext db)=>JsonSerializer.Serialize(new {
        Institutions=await db.Institutions.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),Users=await db.Users.AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),Members=await db.Members.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),Courses=await db.Courses.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),Units=await db.CourseUnits.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),Classes=await db.AcademicClasses.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),Links=await db.ClassUnits.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToListAsync(),Audit=await db.AuditEntries.AsNoTracking().OrderBy(x=>x.Id).ToListAsync()});
}
