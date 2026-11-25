using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolGether.Application.Academic;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Audit;
using SchoolGether.Application.Onboarding;
using SchoolGether.Application.Support;
using SchoolGether.Domain.Academic;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Api.IntegrationTests;
public sealed class AcademicEnrolmentGuardTests
{
    private sealed record Actor(int? UserId,int? InstitutionId,string? Role="InstitutionAdmin"):ICurrentAccount;
    private static void Set(SchoolGetherDbContext db,string name,params object[] args){var property=typeof(SchoolGetherDbContext).GetProperty(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;property.SetValue(db,Activator.CreateInstance(property.PropertyType,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,args,null));}
    private static async Task Denied(Task operation) => Assert.Equal("EnrolmentWriteDenied", (await Assert.ThrowsAsync<InvalidOperationException>(() => operation)).Message);
    [Fact]
    public async Task Enrolment_scope_precedes_every_legacy_return_and_unit_of_work_entry_even_without_pending_changes()
    {
        await using var factory=new IdentityFactory(supportEnabled:true);await factory.SeedAsync();int actor;
        using(var s=factory.Services.CreateScope())actor=await s.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Users.Where(x=>x.Email=="admin-a@example.test").Select(x=>x.Id).SingleAsync();
        using var own=factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>{s.RemoveAll<ICurrentAccount>();s.AddScoped<ICurrentAccount>(_=>new Actor(actor,factory.InstitutionA));}));
        foreach(var other in new[]{"ClassScope","AcademicScope","CourseUnitScope","SupportScope","OnboardingScope"})
        {
            using var services=own.Services.CreateScope();var p=services.ServiceProvider;var db=p.GetRequiredService<SchoolGetherDbContext>();var auth=p.GetRequiredService<AcademicAuthorization>();await using var tx=await db.Database.BeginTransactionAsync();var scope=await auth.RequireEnrolmentAsync("create",1,1);Set(db,"EnrolmentScope",db,scope);
            if(other=="ClassScope")Set(db,other,db,await auth.RequireClassAsync("create",1));else if(other=="AcademicScope")Set(db,other,db,await auth.RequireAsync("create"));else if(other=="CourseUnitScope")Set(db,other,db,await auth.RequireUnitAsync("create",1));else if(other=="SupportScope")Set(db,other,db,await p.GetRequiredService<SupportAuthorization>().RequireAsync("create",null,default));else Set(db,other,db);
            Assert.Equal("EnrolmentWriteDenied",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
            await Denied(p.GetRequiredService<IEnrolmentUnitOfWork>().ExecuteAsync(scope,_=>Task.FromResult(0),default));
            await Denied(p.GetRequiredService<IClassUnitOfWork>().ExecuteAsync(await auth.RequireClassAsync("create",1),_=>Task.FromResult(0),default));
            await Denied(p.GetRequiredService<IAcademicUnitOfWork>().ExecuteAsync(await auth.RequireAsync("create"),_=>Task.FromResult(0),default));
            await Denied(p.GetRequiredService<ICourseUnitOfWork>().ExecuteAsync(await auth.RequireUnitAsync("create",1),_=>Task.FromResult(0),default));
            await Denied(p.GetRequiredService<ISupportUnitOfWork>().ExecuteAsync(await p.GetRequiredService<SupportAuthorization>().RequireAsync("create",null,default),_=>Task.FromResult(0),default));
            await Denied(p.GetRequiredService<IOnboardingUnitOfWork>().ExecuteAsync(_=>Task.FromResult(0),default));
            await Denied(p.GetRequiredService<IAdministrativeUnitOfWork>().ExecuteAsync(_=>Task.FromResult(0),default));
        }
    }
    [Fact]
    public async Task Capability_rejects_forged_swapped_tampered_and_cross_module_writes_and_rolls_back()
    {
        await using var factory=new IdentityFactory(supportEnabled:true);await factory.SeedAsync();using var client=factory.CreateClient();var ids=await AcademicEnrolmentTests.Setup(factory,client);int actor;
        using(var s=factory.Services.CreateScope())actor=await s.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Users.Where(x=>x.Email=="admin-a@example.test").Select(x=>x.Id).SingleAsync();
        using var own=factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>{s.RemoveAll<ICurrentAccount>();s.AddScoped<ICurrentAccount>(_=>new Actor(actor,factory.InstitutionA));}));
        foreach(var attempt in new[]{"unbound","academic","class","unit","support","onboarding","scope","parent-reference","class-reference","enrolment-reference","tenant","course","class-id","student","study-year","academic-year","client","creator","fingerprint","key","slot","state","deleted","created-at","version","parent-notouch","class-notouch","parent-field","class-field","parent-original","class-original","transaction","identity","class-units"})
        {
            using var services=own.Services.CreateScope();var p=services.ServiceProvider;var db=p.GetRequiredService<SchoolGetherDbContext>();var store=p.GetRequiredService<IAcademicEnrolmentStore>();var auth=p.GetRequiredService<AcademicAuthorization>();var scope=await auth.RequireEnrolmentAsync("create",ids.Course,ids.Class);
            var fresh=AcademicEnrolment.Create(factory.InstitutionA,ids.Course,ids.Class,ids.Student,10,"2026/2027",actor,Guid.NewGuid(),DateTime.UtcNow);
            await Assert.ThrowsAnyAsync<InvalidOperationException>(async()=>
            {
                if(attempt=="unbound"){db.Enrolments.Add(fresh);await db.SaveChangesAsync();return;}
                Func<CancellationToken,Task<int>> illegal=_=>{db.Enrolments.Add(fresh);return Task.FromResult(0);};
                if(attempt=="academic"){await p.GetRequiredService<IAcademicUnitOfWork>().ExecuteAsync(await auth.RequireAsync("create"),illegal,default);return;}
                if(attempt=="class"){await p.GetRequiredService<IClassUnitOfWork>().ExecuteAsync(await auth.RequireClassAsync("create",ids.Course),illegal,default);return;}
                if(attempt=="unit"){await p.GetRequiredService<ICourseUnitOfWork>().ExecuteAsync(await auth.RequireUnitAsync("create",ids.Course),illegal,default);return;}
                if(attempt=="support"){await p.GetRequiredService<ISupportUnitOfWork>().ExecuteAsync(await p.GetRequiredService<SupportAuthorization>().RequireAsync("create",null,default),illegal,default);return;}
                if(attempt=="onboarding"){await p.GetRequiredService<IOnboardingUnitOfWork>().ExecuteAsync(illegal,default);return;}
                await p.GetRequiredService<IEnrolmentUnitOfWork>().ExecuteAsync(scope,async token=>
                {
                    var course=(await store.FindCourseAsync(scope,token))!;var value=(await store.FindClassAsync(scope,token))!;var student=(await p.GetRequiredService<IInstitutionStudentDirectory>().FindManyAsync(scope.InstitutionId,[ids.Student],token)).Single();
                    store.Bind(scope,course,value,fresh,student);store.Add(scope,fresh);
                    if(attempt!="parent-notouch")course.TouchEnrolments(course.RowVersion,DateTime.UtcNow);if(attempt!="class-notouch")value.TouchEnrolments(value.RowVersion,DateTime.UtcNow);
                    p.GetRequiredService<IAuditWriter>().Append(new(new(actor,scope.ActorName),"academic.enrolment.create","academic.enrolment:"+fresh.ClientId.ToString("D"),"success","Confirmed",scope.InstitutionId));
                    switch(attempt)
                    {
                        case "scope":store.Add(await auth.RequireEnrolmentAsync("create",ids.Course,ids.Class),fresh);break;
                        case "parent-reference":store.Bind(scope,Course.Create(scope.InstitutionId,actor,Guid.NewGuid(),"F","F",null,DateTime.UtcNow),value,fresh,student);break;
                        case "class-reference":db.AcademicClasses.Add(AcademicClass.Create(scope.InstitutionId,ids.Course,actor,Guid.NewGuid(),"F","F",10,"2026/2027",[1],DateTime.UtcNow));break;
                        case "enrolment-reference":db.Enrolments.Add(AcademicEnrolment.Create(scope.InstitutionId,ids.Course,ids.Class,ids.Student,10,"2026/2027",actor,Guid.NewGuid(),DateTime.UtcNow));break;
                        case "tenant":db.Entry(fresh).Property(x=>x.InstitutionId).CurrentValue=factory.InstitutionB;break;
                        case "course":db.Entry(fresh).Property(x=>x.CourseId).CurrentValue=ids.Course+1;break;
                        case "class-id":db.Entry(fresh).Property(x=>x.ClassId).CurrentValue=ids.Class+1;break;
                        case "student":db.Entry(fresh).Property(x=>x.StudentId).CurrentValue=ids.Student+1;break;
                        case "study-year":db.Entry(fresh).Property(x=>x.StudyYear).CurrentValue=11;break;
                        case "academic-year":db.Entry(fresh).Property(x=>x.AcademicYear).CurrentValue="2027/2028";break;
                        case "client":db.Entry(fresh).Property(x=>x.ClientId).CurrentValue=Guid.NewGuid();break;
                        case "creator":db.Entry(fresh).Property(x=>x.CreatedBy).CurrentValue=actor+1;break;
                        case "fingerprint":db.Entry(fresh).Property(x=>x.CreationFingerprint).CurrentValue=new string('0',64);break;
                        case "key":db.Entry(fresh).Property(x=>x.Id).CurrentValue=700;db.Entry(fresh).Property(x=>x.Id).IsTemporary=false;break;
                        case "slot":db.Entry(fresh).Property(x=>x.ActiveStudentId).CurrentValue=null;break;
                        case "state":db.Entry(fresh).Property(x=>x.IsActive).CurrentValue=false;break;
                        case "deleted":db.Entry(fresh).Property(x=>x.DeletedAt).CurrentValue=DateTime.UtcNow;break;
                        case "created-at":db.Entry(fresh).Property(x=>x.CreatedAt).CurrentValue=DateTime.Now;break;
                        case "version":db.Entry(fresh).Property(x=>x.RowVersion).CurrentValue=10;break;
                        case "parent-field":db.Entry(course).Property(x=>x.Name).CurrentValue="Vedado";break;
                        case "class-field":db.Entry(value).Property(x=>x.Name).CurrentValue="Vedado";break;
                        case "parent-original":db.Entry(course).Property(x=>x.RowVersion).OriginalValue=80;db.Entry(course).Property(x=>x.RowVersion).CurrentValue=81;break;
                        case "class-original":db.Entry(value).Property(x=>x.RowVersion).OriginalValue=80;db.Entry(value).Property(x=>x.RowVersion).CurrentValue=81;break;
                        case "transaction":await db.Database.CurrentTransaction!.RollbackAsync();await db.Database.CurrentTransaction!.DisposeAsync();break;
                        case "identity":(await db.Users.SingleAsync(x=>x.Id==ids.Student)).Name="Vedado";break;
                        case "class-units":db.Entry(await db.ClassUnits.SingleAsync()).Property(x=>x.IsActive).CurrentValue=false;break;
                    }
                    return 0;
                },default);
            });
            db.ChangeTracker.Clear();Assert.Empty(await db.Enrolments.ToListAsync());Assert.Equal(1,(await db.AcademicClasses.SingleAsync()).RowVersion);Assert.Equal(3,(await db.Courses.SingleAsync()).RowVersion);
        }
    }
}
