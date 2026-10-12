using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Audit;
using SchoolGether.Domain.Audit;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class AuditTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task History_RejectsTrackedUpdateAndDeleteInAllSaveOverloads(bool delete, bool asyncSave)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        writer.Append(new AuditEvent(new AuditActor(1, "Operador"), "test", "test", "success", "Teste"));
        await db.SaveChangesAsync();
        var entry = await db.AuditEntries.SingleAsync();
        if (delete) db.AuditEntries.Remove(entry);
        else db.Entry(entry).Property(x => x.Reason).CurrentValue = "Histórico alterado";
        if (asyncSave)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(false));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges(false));
        }
    }

    [Fact]
    public async Task History_RejectsSetBasedEditing()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.AuditEntries.ExecuteDeleteAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.AuditEntries.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Reason, "Alterado")));
    }

    [Theory]
    [InlineData("global")]
    [InlineData("update")]
    [InlineData("institution")]
    [InlineData("account")]
    public async Task FailedAudit_RollsBackBusinessMutationIncludingEarlyIdentitySave(string operation)
    {
        var failure = new FailAuditSave();
        await using var factory = new IdentityFactory();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<SchoolGetherDbContext>(options => options.AddInterceptors(failure))));
        using var client = host.CreateClient();
        await factory.SeedAsync();
        // WithWebHostBuilder cria outro host; a ligação SQLite é a mesma, com os dados isolados da fixture.
        await PlatformAdminTests.Authenticate(client);
        var existing = await PlatformAdminTests.Created(client, "existing@example.test", "audit.read");
        failure.Enabled = true;
        using var response = operation switch {
            "global" => await client.PostAsJsonAsync("/api/v1/platform/admins", PlatformAdminTests.Create("rolled-back@example.test")),
            "update" => await client.PutAsJsonAsync("/api/v1/platform/admins/" + existing.GetProperty("id").GetInt32(),
                new { Name = "Não guardar", IsActive = false, Permissions = Array.Empty<string>(), RowVersion = 1, Reason = "Teste" }),
            "institution" => await client.PostAsJsonAsync("/api/v1/institutions", new { Name = "Não guardar" }),
            _ => await client.PostAsJsonAsync("/api/v1/identity/users", new { Email = "rolled-back@example.test",
                Password = IdentityFactory.Password, Name = "Não guardar", Role = "Teacher", InstitutionId = factory.InstitutionA }) };
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.False(await db.Users.AnyAsync(x => x.Email == "rolled-back@example.test"));
        Assert.False(await db.Institutions.AnyAsync(x => x.Name == "Não guardar"));
        var original = await db.Users.SingleAsync(x => x.Email == "existing@example.test");
        Assert.True(original.IsActive);
        Assert.Equal(1, original.RowVersion);
        Assert.Equal("Administrador", original.Name);
        Assert.DoesNotContain(await db.AuditEntries.ToListAsync(), x => x.Reason == "Teste" || x.Target == "rolled-back@example.test");
    }

    [Fact]
    public async Task Writer_DropsSecretsAndIndependentRefusalDoesNotSavePendingBusinessData()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var writer = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        writer.Append(new AuditEvent(new AuditActor(1, "Operador"), "test", "test", "success", "Teste", After: new Dictionary<string, object?> {
            ["name"] = "Permitido", ["Password"] = "segredo", ["SecurityStamp"] = "segredo", ["token"] = "segredo",
            ["connectionString"] = "segredo", ["permissions"] = new[] { "audit.read" } }));
        await db.SaveChangesAsync();
        var entry = await db.AuditEntries.SingleAsync();
        Assert.Contains("Permitido", entry.After);
        Assert.DoesNotContain("segredo", entry.After);
        var user = await db.Users.SingleAsync(x => x.Email == "platform@example.test");
        user.Name = "Não guardar";
        await writer.AppendIndependentAsync(new AuditEvent(new AuditActor(1, "Operador"), "test.denied", "test", "refused", "Forbidden"), default);
        db.ChangeTracker.Clear();
        Assert.Equal("Plataforma", (await db.Users.SingleAsync(x => x.Id == user.Id)).Name);
        Assert.Equal(2, await db.AuditEntries.CountAsync());
    }

    private sealed class FailAuditSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(x => x.State == EntityState.Added))
                throw new InvalidOperationException("Falha injetada ao gravar auditoria.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
