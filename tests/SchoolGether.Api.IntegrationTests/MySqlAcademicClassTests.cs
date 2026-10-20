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

public sealed class MySqlAcademicClassTests
{
    // Intercetor apenas do teste: ambos os serviços já leram o pai quando chegam a SaveChanges.
    private sealed class SaveBarrier : SaveChangesInterceptor
    {
        private int mode;
        private int arrivals;
        private TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentBag<long> ParentVersions { get; } = [];
        public void Arm(int value) { mode=value; arrivals=0; ParentVersions.Clear(); released=new(TaskCreationOptions.RunContinuationsAsynchronously); }
        public void Disarm() => mode=0;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct=default)
        {
            var db=eventData.Context!;
            var applies=mode==1 && (db.ChangeTracker.Entries<AcademicClass>().Any(e=>e.State==EntityState.Added) || db.ChangeTracker.Entries<CourseUnit>().Any(e=>e.Property(x=>x.StudyYear).IsModified)) || mode==2 && db.ChangeTracker.Entries<AcademicClass>().Any(e=>e.State==EntityState.Added);
            if(!applies)return result;
            ParentVersions.Add(db.ChangeTracker.Entries<Course>().Single(e=>e.State==EntityState.Modified).Property(x=>x.RowVersion).OriginalValue);
            if(Interlocked.Increment(ref arrivals)==2)released.TrySetResult();
            await released.Task.WaitAsync(TimeSpan.FromSeconds(20),ct);return result;
        }
    }
    [MySqlFact]
    public async Task Upgrade_ten_to_eleven_preserves_legacy_units_and_enforces_year_barrier_replay_fk_uniques_and_atomicity()
    {
        var barrier=new SaveBarrier();
        await using var factory=new IdentityFactory(mySqlConnectionString:Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"),migrationTarget:"20261020092303_AcademicCourseUnits");
        using var instrumented=factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>s.ConfigureDbContext<SchoolGetherDbContext>(o=>o.AddInterceptors(barrier))));
        await factory.SeedAsync();using var client=instrumented.CreateClient();await AcademicCourseTests.Login(client,"admin-a@example.test");
        var course=await AcademicCourseUnitTests.Course(client);var other=await AcademicCourseUnitTests.Course(client,"OTHER");var key=Guid.NewGuid();int oldId;
        const string legacyFingerprint="4C397FF724F2286976018395F15B2167B6B8EF69A1BABF5440C22E2AF61B095B";
        using(var services=factory.Services.CreateScope())
        {
            var db=services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();Assert.Equal(10,(await db.Database.GetAppliedMigrationsAsync()).Count());
            var actor=await db.Users.Where(x=>x.Email=="admin-a@example.test").Select(x=>x.Id).SingleAsync();var now=DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO academic_course_unit (institution_id,course_id,client_id,code,name,syllabus,is_active,row_version,created_at,created_by,creation_fingerprint) VALUES ({factory.InstitutionA},{course},{key},{"OLD"},{"Legacy"},{"Programa"},{true},{1},{now},{actor},{legacyFingerprint})");
            var courses=JsonSerializer.Serialize(await db.Courses.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToListAsync());var institutions=JsonSerializer.Serialize(await db.Institutions.AsNoTracking().OrderBy(x=>x.Id).ToListAsync());var users=JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x=>x.Id).ToListAsync());var members=JsonSerializer.Serialize(await db.Members.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToListAsync());var audits=JsonSerializer.Serialize(await db.AuditEntries.AsNoTracking().OrderBy(x=>x.Id).ToListAsync());
            await db.Database.MigrateAsync();Assert.Equal(11,(await db.Database.GetAppliedMigrationsAsync()).Count());Assert.Empty(await db.Database.GetPendingMigrationsAsync());Assert.Equal("MySql.EntityFrameworkCore",db.Database.ProviderName);
            Assert.Equal(courses,JsonSerializer.Serialize(await db.Courses.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToListAsync()));Assert.Equal(institutions,JsonSerializer.Serialize(await db.Institutions.AsNoTracking().OrderBy(x=>x.Id).ToListAsync()));Assert.Equal(users,JsonSerializer.Serialize(await db.Users.AsNoTracking().OrderBy(x=>x.Id).ToListAsync()));Assert.Equal(members,JsonSerializer.Serialize(await db.Members.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToListAsync()));Assert.Equal(audits,JsonSerializer.Serialize(await db.AuditEntries.AsNoTracking().OrderBy(x=>x.Id).ToListAsync()));
            var old=await db.CourseUnits.IgnoreQueryFilters().SingleAsync();oldId=old.Id;Assert.Null(old.StudyYear);Assert.Equal(legacyFingerprint,old.CreationFingerprint);Assert.Equal("Programa",old.Syllabus);Assert.Equal(key,old.ClientId);Assert.Equal(1,old.RowVersion);Assert.Equal(DateTimeKind.Utc,old.CreatedAt.Kind);
        }
        var units=AcademicCourseUnitTests.Route(course);var route=AcademicClassTests.Route(course);
        await AcademicCourseTests.Read(await client.PutAsJsonAsync($"{units}/{oldId}",new {RowVersion=1,Code="OLD",Name="Legacy",Syllabus="Programa",StudyYear=10}));
        var replay=await AcademicCourseTests.Read(await client.PostAsJsonAsync(units,new {ClientId=key,Code="OLD",Name="Legacy",Syllabus="Programa"}));Assert.True(replay.GetProperty("replay").GetBoolean());Assert.Equal(10,replay.GetProperty("studyYear").GetInt32());
        var raceUnit=await AcademicClassTests.Unit(client,course,"RACE");
        barrier.Arm(1);
        var race=await Task.WhenAll(client.PutAsJsonAsync($"{units}/{raceUnit}",new {RowVersion=1,Code="RACE",Name="Ano11",StudyYear=11}),client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),Code="RACE",Name="Turma10",StudyYear=10,AcademicYear="2026/2027",UnitIds=new[]{raceUnit}}));
        barrier.Disarm();Assert.Equal(2,barrier.ParentVersions.Count);Assert.Single(barrier.ParentVersions.Distinct());Assert.Single(race,x=>x.IsSuccessStatusCode);Assert.Single(race,x=>x.StatusCode==HttpStatusCode.Conflict);foreach(var response in race)response.Dispose();
        using(var services=factory.Services.CreateScope())
        {
            var db=services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();Assert.False(await db.ClassUnits.IgnoreQueryFilters().Include(x=>x.Class).Include(x=>x.Unit).AnyAsync(x=>x.Class.StudyYear!=x.Unit.StudyYear));
        }
        var replayKey=Guid.NewGuid();var body=new {ClientId=replayKey,Code="10A",Name="Turma",StudyYear=10,AcademicYear="2026/2027",UnitIds=new[]{oldId}};
        barrier.Arm(2);var creates=await Task.WhenAll(client.PostAsJsonAsync(route,body),client.PostAsJsonAsync(route,body));barrier.Disarm();
        Assert.Equal(2,barrier.ParentVersions.Count);Assert.Single(barrier.ParentVersions.Distinct());Assert.Single(creates,x=>x.StatusCode==HttpStatusCode.Created);Assert.Single(creates,x=>x.StatusCode==HttpStatusCode.OK);
        var created=await creates.Single(x=>x.StatusCode==HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>();var id=created.GetProperty("id").GetInt32();foreach(var response in creates)response.Dispose();Assert.Equal(DateTimeKind.Utc,created.GetProperty("createdAt").GetDateTime().Kind);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"{units}/{oldId}",new {RowVersion=2,Code="OLD",Name="Vedada",StudyYear=11})).StatusCode);
        using(var services=factory.Services.CreateScope())
        {
            var db=services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();Assert.Equal(1,await db.AuditEntries.CountAsync(x=>x.Target=="academic.class:"+replayKey.ToString("D")&&x.Result=="success"));
            var fk=await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic_class SET institution_id={factory.InstitutionB} WHERE id={id}"));Assert.Contains(fk.Number,new[]{1216,1217,1451,1452});
            fk=await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE academic_class_unit SET course_id={other} WHERE class_id={id}"));Assert.Contains(fk.Number,new[]{1216,1452});
            var duplicate=await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO academic_class_unit (institution_id,course_id,class_id,unit_id,is_active,row_version,created_at,created_by) SELECT institution_id,course_id,class_id,unit_id,is_active,row_version,created_at,created_by FROM academic_class_unit WHERE class_id={id}"));Assert.Equal(1062,duplicate.Number);
        }
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),Code="10A",Name="Ocupada",StudyYear=10,AcademicYear="2026/2027",UnitIds=new[]{oldId}})).StatusCode);
        var before=await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}"));
        using(var services=factory.Services.CreateScope())await services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_class_audit BEFORE INSERT ON audit_entry FOR EACH ROW BEGIN IF NEW.target LIKE 'academic.class:%' AND NEW.result='success' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='class audit unavailable'; END IF; END");
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError,(await client.PutAsJsonAsync($"{route}/{id}",new {RowVersion=1,Code="ROLLBACK",Name="Perdida",UnitIds=new[]{oldId}})).StatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError,(await client.PostAsJsonAsync(route,new {ClientId=Guid.NewGuid(),Code="ROLLBACK",Name="Perdida",StudyYear=10,AcademicYear="2026/2027",UnitIds=new[]{oldId}})).StatusCode);
            Assert.Equal(before.GetRawText(),(await AcademicCourseTests.Read(await client.GetAsync($"{AcademicCourseTests.Route}/{course}"))).GetRawText());Assert.Equal(created.GetRawText(),(await AcademicCourseTests.Read(await client.GetAsync($"{route}/{id}"))).GetRawText());
            using var services=factory.Services.CreateScope();var db=services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();Assert.False(await db.AcademicClasses.IgnoreQueryFilters().AnyAsync(x=>x.Code=="ROLLBACK"));Assert.Equal(legacyFingerprint,(await db.CourseUnits.IgnoreQueryFilters().SingleAsync(x=>x.Id==oldId)).CreationFingerprint);Assert.All(await db.AuditEntries.Where(x=>x.Target=="academic.class:"+replayKey.ToString("D")&&x.Result=="success").ToListAsync(),x=>{Assert.Null(x.Before);Assert.Null(x.After);});
        }
        finally {using var services=factory.Services.CreateScope();await services.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Database.ExecuteSqlRawAsync("DROP TRIGGER reject_class_audit");}
    }
}
