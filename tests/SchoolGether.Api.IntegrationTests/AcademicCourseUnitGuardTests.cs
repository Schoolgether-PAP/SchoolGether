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

public sealed class AcademicCourseUnitGuardTests
{
    private sealed record Actor(int? UserId, int? InstitutionId, string? Role = "InstitutionAdmin") : ICurrentAccount;
    [Fact]
    public async Task Capabilities_reject_swapped_references_identity_transactions_and_other_modules()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync(); int actorId;
        using (var seed = factory.Services.CreateScope()) actorId = await seed.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Users.Where(x => x.Email == "admin-a@example.test").Select(x => x.Id).SingleAsync();
        using var scopedFactory = factory.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<ICurrentAccount>(); s.AddScoped<ICurrentAccount>(_ => new Actor(actorId, factory.InstitutionA)); })); int first; int second; int unitId; int otherId;
        using (var setup = scopedFactory.Services.CreateScope())
        {
            var courses = setup.ServiceProvider.GetRequiredService<AcademicCourseService>(); first = (await courses.CreateAsync(new(Guid.NewGuid(), "A", "Primeiro", null), default)).Id; second = (await courses.CreateAsync(new(Guid.NewGuid(), "B", "Segundo", null), default)).Id;
            var units = setup.ServiceProvider.GetRequiredService<AcademicCourseUnitService>(); unitId = (await units.CreateAsync(first, new(Guid.NewGuid(), "U", "Inicial", null), default)).Id; otherId = (await units.CreateAsync(first, new(Guid.NewGuid(), "V", "Outra", null), default)).Id;
        }
        foreach (var attempt in new[] { "unbound", "academic", "support", "onboarding", "scope", "parent-reference", "unit-reference", "tenant", "parent", "author", "client", "fingerprint", "module", "delete", "parent-field", "parent-archive", "notouch", "transaction", "create-client" })
        {
            using var services = scopedFactory.Services.CreateScope(); var p = services.ServiceProvider; var db = p.GetRequiredService<SchoolGetherDbContext>(); var store = p.GetRequiredService<IAcademicCourseUnitStore>(); var auth = p.GetRequiredService<AcademicAuthorization>(); var work = p.GetRequiredService<ICourseUnitOfWork>(); var scope = await auth.RequireUnitAsync(attempt == "create-client" ? "create" : "update", first, attempt == "create-client" ? null : unitId);
            await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
            {
                var forged = CourseUnit.Create(factory.InstitutionA, first, actorId, Guid.NewGuid(), "FORGED", "Vedada", null, DateTime.UtcNow);
                if (attempt == "unbound") { db.CourseUnits.Add(forged); await db.SaveChangesAsync(); }
                else if (attempt == "academic") await p.GetRequiredService<IAcademicUnitOfWork>().ExecuteAsync(await auth.RequireAsync("update", first), token => { db.CourseUnits.Add(forged); return Task.FromResult(0); }, default);
                else if (attempt == "support") await p.GetRequiredService<ISupportUnitOfWork>().ExecuteAsync(await p.GetRequiredService<SupportAuthorization>().RequireAsync("create", null, default), token => { db.CourseUnits.Add(forged); return Task.FromResult(0); }, default);
                else if (attempt == "onboarding") await p.GetRequiredService<IOnboardingUnitOfWork>().ExecuteAsync(token => { db.CourseUnits.Add(forged); return Task.FromResult(0); }, default);
                else await work.ExecuteAsync(scope, async token =>
                {
                    var parent = (await store.FindCourseAsync(scope, token))!; var unit = attempt == "create-client" ? forged : (await store.FindAsync(scope, unitId, token))!;
                    if (attempt == "create-client") store.Add(scope, parent, unit); else store.Bind(scope, parent, unit);
                    if (attempt != "notouch") parent.TouchUnits(parent.RowVersion, DateTime.UtcNow);
                    if (attempt != "create-client") unit.Update(1, "U", "Vedada", null, DateTime.UtcNow);
                    switch (attempt)
                    {
                        case "scope": store.Bind(await auth.RequireUnitAsync("update", first, unitId), parent, unit); break;
                        case "parent-reference": store.Bind(scope, (await db.Courses.SingleAsync(x => x.Id == second, token)), unit); break;
                        case "unit-reference": (await db.CourseUnits.SingleAsync(x => x.Id == otherId, token)).Update(1, "V", "Vedada", null, DateTime.UtcNow); break;
                        case "tenant": db.Entry(unit).Property(x => x.InstitutionId).CurrentValue = factory.InstitutionB; break;
                        case "parent": db.Entry(unit).Property(x => x.CourseId).CurrentValue = second; break;
                        case "author": db.Entry(unit).Property(x => x.CreatedBy).CurrentValue = actorId + 1; break;
                        case "client": case "create-client": db.Entry(unit).Property(x => x.ClientId).CurrentValue = Guid.NewGuid(); break;
                        case "fingerprint": db.Entry(unit).Property(x => x.CreationFingerprint).CurrentValue = new string('0', 64); break;
                        case "module": db.Institutions.Add(new Institution("Vedada", actorId)); break;
                        case "delete": db.CourseUnits.Remove(unit); break;
                        case "parent-field": db.Entry(parent).Property(x => x.Name).CurrentValue = "Vedado"; break;
                        case "parent-archive": parent.Archive(parent.RowVersion, DateTime.UtcNow); break;
                        case "transaction": await db.Database.UseTransactionAsync(null, token); break;
                    }
                    return 0;
                }, default);
            });
            db.ChangeTracker.Clear(); Assert.Equal(2, await db.CourseUnits.CountAsync()); Assert.Equal("Inicial", (await db.CourseUnits.SingleAsync(x => x.Id == unitId)).Name); Assert.Equal(3, (await db.Courses.SingleAsync(x => x.Id == first)).RowVersion);
        }
    }
    [Fact]
    public async Task Mixed_capabilities_are_rejected_even_without_changes_before_legacy_early_returns()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        int actorId;
        using (var seed = factory.Services.CreateScope()) actorId = await seed.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Users.Where(x => x.Email == "admin-a@example.test").Select(x => x.Id).SingleAsync();
        using var scopedFactory = factory.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<ICurrentAccount>(); s.AddScoped<ICurrentAccount>(_ => new Actor(actorId, factory.InstitutionA)); }));
        foreach (var other in new[] { "AcademicScope", "SupportScope", "OnboardingScope" })
        {
            using var services = scopedFactory.Services.CreateScope(); var p = services.ServiceProvider;
            var db = p.GetRequiredService<SchoolGetherDbContext>(); var auth = p.GetRequiredService<AcademicAuthorization>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var authorized = await auth.RequireUnitAsync("create", 1);
            SetScope("CourseUnitScope", db, authorized);
            if (other == "AcademicScope") SetScope(other, db, await auth.RequireAsync("create"));
            else if (other == "SupportScope") SetScope(other, db, await p.GetRequiredService<SupportAuthorization>().RequireAsync("create", null, default));
            else SetScope(other, db);
            Assert.Equal("CourseUnitWriteDenied", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
            await Assert.ThrowsAsync<InvalidOperationException>(() => p.GetRequiredService<ICourseUnitOfWork>().ExecuteAsync(authorized, _ => Task.FromResult(0), default));
            await transaction.RollbackAsync();
            void SetScope(string name, params object[] args)
            {
                var property = typeof(SchoolGetherDbContext).GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                property.SetValue(db, Activator.CreateInstance(property.PropertyType, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, args, null));
            }
        }
    }
}
