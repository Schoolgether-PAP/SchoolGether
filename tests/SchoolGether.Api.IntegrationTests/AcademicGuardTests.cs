using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolGether.Application.Academic;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Support;
using SchoolGether.Application.Onboarding;
using SchoolGether.Domain.Academic;
using SchoolGether.Domain.Institutions;
using SchoolGether.Domain.Support;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class AcademicGuardTests
{
    private sealed record Actor(int? UserId, int? InstitutionId, string? Role = "InstitutionAdmin") : ICurrentAccount;
    [Fact]
    public async Task Ef_guard_binds_reference_scope_tenant_immutable_identity_and_module()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        int actorId;
        using (var seed = factory.Services.CreateScope()) actorId = await seed.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Users.Where(x => x.Email == "admin-a@example.test").Select(x => x.Id).SingleAsync();
        using var scopedFactory = factory.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<ICurrentAccount>(); s.AddScoped<ICurrentAccount>(_ => new Actor(actorId, factory.InstitutionA)); }));
        int first; int second;
        using (var setup = scopedFactory.Services.CreateScope())
        {
            var service = setup.ServiceProvider.GetRequiredService<AcademicCourseService>();
            first = (await service.CreateAsync(new(Guid.NewGuid(), "A", "Primeiro", null), default)).Id;
            second = (await service.CreateAsync(new(Guid.NewGuid(), "B", "Segundo", null), default)).Id;
        }
        foreach (var attempt in new[] { "unbound", "foreign", "reference", "scope", "tenant", "author", "client", "fingerprint", "module", "delete", "support", "onboarding", "transaction" })
        {
            using var services = scopedFactory.Services.CreateScope(); var provider = services.ServiceProvider;
            var db = provider.GetRequiredService<SchoolGetherDbContext>(); var store = provider.GetRequiredService<IAcademicCourseStore>();
            var work = provider.GetRequiredService<IAcademicUnitOfWork>(); var auth = provider.GetRequiredService<AcademicAuthorization>();
            var scope = await auth.RequireAsync("update", first);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                if (attempt == "support")
                {
                    var supportScope = await provider.GetRequiredService<SupportAuthorization>().RequireAsync("create", null, default);
                    await provider.GetRequiredService<ISupportUnitOfWork>().ExecuteAsync(supportScope, token =>
                    { db.Courses.Add(Course.Create(factory.InstitutionA, actorId, Guid.NewGuid(), "FORGED", "Vedado", null, DateTime.UtcNow)); return Task.FromResult(0); }, default);
                }
                else if (attempt == "onboarding")
                    await provider.GetRequiredService<IOnboardingUnitOfWork>().ExecuteAsync(token =>
                    { db.Courses.Add(Course.Create(factory.InstitutionA, actorId, Guid.NewGuid(), "FORGED", "Vedado", null, DateTime.UtcNow)); return Task.FromResult(0); }, default);
                else if (attempt == "unbound")
                { db.Courses.Add(Course.Create(factory.InstitutionA, actorId, Guid.NewGuid(), "FORGED", "Vedado", null, DateTime.UtcNow)); await db.SaveChangesAsync(); }
                else await work.ExecuteAsync(scope, async token =>
                {
                    var value = (await store.FindAsync(scope, first, token))!; store.Bind(scope, value);
                    if (attempt is "tenant" or "author" or "client" or "fingerprint" or "transaction") value.Update(1, "A", "Alterado", null, DateTime.UtcNow);
                    switch (attempt)
                    {
                        case "foreign": store.Bind(scope, Course.Create(factory.InstitutionB, actorId, Guid.NewGuid(), "F", "F", null, DateTime.UtcNow)); break;
                        case "reference": (await store.FindAsync(scope, second, token))!.Update(1, "C", "Vedado", null, DateTime.UtcNow); break;
                        case "scope": store.Bind(await auth.RequireAsync("update", first), value); break;
                        case "tenant": db.Entry(value).Property(x => x.InstitutionId).CurrentValue = factory.InstitutionB; break;
                        case "author": db.Entry(value).Property(x => x.CreatedBy).CurrentValue = actorId + 100; break;
                        case "client": db.Entry(value).Property(x => x.ClientId).CurrentValue = Guid.NewGuid(); break;
                        case "fingerprint": db.Entry(value).Property(x => x.CreationFingerprint).CurrentValue = new string('0',64); break;
                        case "module": db.Institutions.Add(new Institution("Vedado", actorId)); break;
                        case "delete": db.Courses.Remove(value); break;
                        case "transaction": await db.Database.UseTransactionAsync(null, token); break;
                    }
                    return 0;
                }, default);
            });
            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.Courses.CountAsync());
            Assert.Equal("Primeiro", (await db.Courses.SingleAsync(x => x.Id == first)).Name);
        }
        using (var verify = scopedFactory.Services.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            var options = verify.ServiceProvider.GetRequiredService<DbContextOptions<SchoolGetherDbContext>>();
            await using var teacher = new SchoolGetherDbContext(options, new Actor(actorId, factory.InstitutionA, "Teacher")); Assert.Empty(await teacher.Courses.ToListAsync());
            await using var foreign = new SchoolGetherDbContext(options, new Actor(actorId, factory.InstitutionB)); Assert.Empty(await foreign.Courses.ToListAsync());
            var value = await db.Courses.SingleAsync(x => x.Id == first); value.Update(1,"A","Fora da transação",null, DateTime.UtcNow);
            Assert.Equal("AcademicWriteDenied", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        }
    }
}
