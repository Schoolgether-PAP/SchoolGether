using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolGether.Application.Academic;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Onboarding;
using SchoolGether.Application.Support;
using SchoolGether.Domain.Academic;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Api.IntegrationTests;

public sealed class AcademicClassGuardTests
{
    private sealed record Actor(int? UserId,int? InstitutionId,string? Role="InstitutionAdmin"):ICurrentAccount;
    [Fact]
    public async Task Capability_rejects_unbound_swapped_tampered_ineligible_and_unserialized_aggregate_writes()
    {
        await using var factory=new IdentityFactory(supportEnabled:true); await factory.SeedAsync(); int actor;
        using(var seed=factory.Services.CreateScope()) actor=await seed.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Users.Where(x=>x.Email=="admin-a@example.test").Select(x=>x.Id).SingleAsync();
        using var own=factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>{s.RemoveAll<ICurrentAccount>();s.AddScoped<ICurrentAccount>(_=>new Actor(actor,factory.InstitutionA));}));
        int course,a,b,eleven,classId;
        using(var setup=own.Services.CreateScope())
        {
            var p=setup.ServiceProvider; course=(await p.GetRequiredService<AcademicCourseService>().CreateAsync(new(Guid.NewGuid(),"A","Curso",null),default)).Id;
            var service=p.GetRequiredService<AcademicCourseUnitService>(); a=(await service.CreateAsync(course,new(Guid.NewGuid(),"A","A",null,10),default)).Id;b=(await service.CreateAsync(course,new(Guid.NewGuid(),"B","B",null,10),default)).Id;eleven=(await service.CreateAsync(course,new(Guid.NewGuid(),"C","C",null,11),default)).Id;
            classId=(await p.GetRequiredService<AcademicClassService>().CreateAsync(course,new(Guid.NewGuid(),"10A","Inicial",10,"2026/2027",[a]),default)).Id;
        }
        foreach(var attempt in new[]{"unbound","academic","unit","support","onboarding","scope","class-reference","parent-reference","link-reference","unit-reference","tenant","course","author","client","fingerprint","year","academic-year","link-tenant","link-course","link-unit","link-author","link-delete","class-delete","parent-field","parent-archive","notouch","transaction","empty-selection","ineligible","module","create-client","create-key","class-notouch","version-original","parent-version-original"})
        {
            using var services=own.Services.CreateScope();var p=services.ServiceProvider;var db=p.GetRequiredService<SchoolGetherDbContext>();var store=p.GetRequiredService<IAcademicClassStore>();var auth=p.GetRequiredService<AcademicAuthorization>();var work=p.GetRequiredService<IClassUnitOfWork>();var create=attempt.StartsWith("create-");var scope=await auth.RequireClassAsync(create?"create":"update",course,create?null:classId);
            await Assert.ThrowsAnyAsync<InvalidOperationException>(async()=>
            {
                var forged=AcademicClass.Create(factory.InstitutionA,course,actor,Guid.NewGuid(),"F","Vedada",10,"2026/2027",[a],DateTime.UtcNow);
                if(attempt=="unbound") {db.AcademicClasses.Add(forged);await db.SaveChangesAsync();}
                else if(attempt=="academic") await p.GetRequiredService<IAcademicUnitOfWork>().ExecuteAsync(await auth.RequireAsync("update",course),_=>{db.AcademicClasses.Add(forged);return Task.FromResult(0);},default);
                else if(attempt=="unit") await p.GetRequiredService<ICourseUnitOfWork>().ExecuteAsync(await auth.RequireUnitAsync("update",course,a),_=>{db.AcademicClasses.Add(forged);return Task.FromResult(0);},default);
                else if(attempt=="support") await p.GetRequiredService<ISupportUnitOfWork>().ExecuteAsync(await p.GetRequiredService<SupportAuthorization>().RequireAsync("create",null,default),_=>{db.AcademicClasses.Add(forged);return Task.FromResult(0);},default);
                else if(attempt=="onboarding") await p.GetRequiredService<IOnboardingUnitOfWork>().ExecuteAsync(_=>{db.AcademicClasses.Add(forged);return Task.FromResult(0);},default);
                else await work.ExecuteAsync(scope,async token=>
                {
                    var parent=(await store.FindCourseAsync(scope,token))!;var value=create?forged:(await store.FindAsync(scope,classId,token))!;var links=create?new List<AcademicClassUnit>():await store.LinksAsync(scope,value,token);var units=await store.UnitsAsync(scope,[attempt=="ineligible"?eleven:a],token);
                    store.Bind(scope,parent,value,links,units);
                    if(create){store.Add(scope,value);store.AddLink(scope,AcademicClassUnit.Create(value,units[0],actor,DateTime.UtcNow));}else if(attempt!="class-notouch") value.Update(1,"10A","Vedada",DateTime.UtcNow);
                    if(attempt!="notouch")parent.TouchClasses(parent.RowVersion,DateTime.UtcNow);
                    switch(attempt)
                    {
                        case "scope":store.Bind(await auth.RequireClassAsync("update",course,classId),parent,value,links,units);break;
                        case "class-reference":db.AcademicClasses.Add(forged);break;
                        case "parent-reference":store.Bind(scope,Course.Create(factory.InstitutionA,actor,Guid.NewGuid(),"X","X",null,DateTime.UtcNow),value,links,units);break;
                        case "link-reference":db.ClassUnits.Add(AcademicClassUnit.Create(value,(await store.UnitsAsync(scope,[b],token))[0],actor,DateTime.UtcNow));break;
                        case "unit-reference":(await store.UnitsAsync(scope,[b],token))[0].Update(1,"B","Vedada",null,DateTime.UtcNow);break;
                        case "tenant":db.Entry(value).Property(x=>x.InstitutionId).CurrentValue=factory.InstitutionB;break;
                        case "course":db.Entry(value).Property(x=>x.CourseId).CurrentValue=course+1;break;
                        case "author":db.Entry(value).Property(x=>x.CreatedBy).CurrentValue=actor+1;break;
                        case "client":case "create-client":db.Entry(value).Property(x=>x.ClientId).CurrentValue=Guid.NewGuid();break;
                        case "create-key":db.Entry(value).Property(x=>x.Id).CurrentValue=789;db.Entry(value).Property(x=>x.Id).IsTemporary=false;break;
                        case "fingerprint":db.Entry(value).Property(x=>x.CreationFingerprint).CurrentValue=new string('0',64);break;
                        case "year":db.Entry(value).Property(x=>x.StudyYear).CurrentValue=11;break;
                        case "academic-year":db.Entry(value).Property(x=>x.AcademicYear).CurrentValue="2027/2028";break;
                        case "link-tenant":db.Entry(links[0]).Property(x=>x.InstitutionId).CurrentValue=factory.InstitutionB;break;
                        case "link-course":db.Entry(links[0]).Property(x=>x.CourseId).CurrentValue=course+1;break;
                        case "link-unit":db.Entry(links[0]).Property(x=>x.UnitId).CurrentValue=b;break;
                        case "link-author":db.Entry(links[0]).Property(x=>x.CreatedBy).CurrentValue=actor+1;break;
                        case "link-delete":db.ClassUnits.Remove(links[0]);break;
                        case "class-delete":db.AcademicClasses.Remove(value);break;
                        case "parent-field":db.Entry(parent).Property(x=>x.Name).CurrentValue="Vedado";break;
                        case "parent-archive":parent.Archive(parent.RowVersion,DateTime.UtcNow);break;
                        case "transaction":await db.Database.UseTransactionAsync(null,token);break;
                        case "empty-selection":links[0].SetSelected(false,DateTime.UtcNow);break;
                        case "version-original":db.Entry(value).Property(x=>x.RowVersion).OriginalValue=89;db.Entry(value).Property(x=>x.RowVersion).CurrentValue=90;break;
                        case "parent-version-original":db.Entry(parent).Property(x=>x.RowVersion).OriginalValue=89;db.Entry(parent).Property(x=>x.RowVersion).CurrentValue=90;break;
                        case "module":db.Institutions.Add(new Institution("Vedada",actor));break;
                    }
                    return 0;
                },default);
            });
            db.ChangeTracker.Clear();Assert.Single(await db.AcademicClasses.ToListAsync());Assert.Single(await db.ClassUnits.ToListAsync());Assert.Equal("Inicial",(await db.AcademicClasses.SingleAsync()).Name);Assert.Equal(5,(await db.Courses.SingleAsync()).RowVersion);
        }
    }
    [Fact]
    public async Task Class_scope_is_exclusive_before_all_legacy_returns_and_unit_of_work_entry_points()
    {
        await using var factory=new IdentityFactory(supportEnabled:true);await factory.SeedAsync();int actor;
        using(var seed=factory.Services.CreateScope()) actor=await seed.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Users.Where(x=>x.Email=="admin-a@example.test").Select(x=>x.Id).SingleAsync();
        using var own=factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>{s.RemoveAll<ICurrentAccount>();s.AddScoped<ICurrentAccount>(_=>new Actor(actor,factory.InstitutionA));}));
        foreach(var other in new[]{"AcademicScope","CourseUnitScope","SupportScope","OnboardingScope"})
        {
            using var services=own.Services.CreateScope();var p=services.ServiceProvider;var db=p.GetRequiredService<SchoolGetherDbContext>();var auth=p.GetRequiredService<AcademicAuthorization>();await using var tx=await db.Database.BeginTransactionAsync();var scope=await auth.RequireClassAsync("create",1);Set("ClassScope",db,scope);
            if(other=="AcademicScope")Set(other,db,await auth.RequireAsync("create"));else if(other=="CourseUnitScope")Set(other,db,await auth.RequireUnitAsync("create",1));else if(other=="SupportScope")Set(other,db,await p.GetRequiredService<SupportAuthorization>().RequireAsync("create",null,default));else Set(other,db);
            Assert.Equal("ClassWriteDenied",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>p.GetRequiredService<IClassUnitOfWork>().ExecuteAsync(scope,_=>Task.FromResult(0),default));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>p.GetRequiredService<IAcademicUnitOfWork>().ExecuteAsync(auth.RequireAsync("create").Result,_=>Task.FromResult(0),default));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>p.GetRequiredService<ICourseUnitOfWork>().ExecuteAsync(auth.RequireUnitAsync("create",1).Result,_=>Task.FromResult(0),default));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>p.GetRequiredService<ISupportUnitOfWork>().ExecuteAsync(p.GetRequiredService<SupportAuthorization>().RequireAsync("create",null,default).Result,_=>Task.FromResult(0),default));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>p.GetRequiredService<IOnboardingUnitOfWork>().ExecuteAsync(_=>Task.FromResult(0),default));
            await tx.RollbackAsync();
            void Set(string name,params object[] args){var property=typeof(SchoolGetherDbContext).GetProperty(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;property.SetValue(db,Activator.CreateInstance(property.PropertyType,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,args,null));}
        }
    }
}
