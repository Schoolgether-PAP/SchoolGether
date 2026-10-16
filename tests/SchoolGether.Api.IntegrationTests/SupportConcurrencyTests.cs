using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolGether.Application.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Platform;
using SchoolGether.Application.Support;
using SchoolGether.Domain.Support;
using SchoolGether.Domain.Audit;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class SupportConcurrencyTests
{
    [Fact]
    public async Task AuditCompletionFailure_RollsBackAlreadyWrittenRows()
    {
        var failure = new FailAfterSave();
        await using var factory = new IdentityFactory(supportEnabled: true);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<SchoolGetherDbContext>(options => options.AddInterceptors(failure))));
        using var client = host.CreateClient(); await factory.SeedAsync();
        await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        failure.Enabled = true;
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync(SupportAuthorizationTests.Route, SupportAuthorizationTests.Create())).StatusCode);
        Assert.True(failure.ObservedSavedRows);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Empty(await db.SupportTickets.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.SupportMessages.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.AuditEntries.Where(x => x.Action.StartsWith("support.")).ToListAsync());
    }

    [Fact]
    public async Task NotificationFailure_DoesNotTurnCommittedCreationIntoHttpFailure()
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => {
            services.RemoveAll<ISupportNotifier>(); services.AddSingleton<ISupportNotifier, FailingNotifier>();
        }));
        using var client = host.CreateClient(); await factory.SeedAsync();
        await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var clientId = Guid.NewGuid(); await SupportAuthorizationTests.Ticket(client, clientId);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(SupportAuthorizationTests.Route, SupportAuthorizationTests.Create(clientId))).StatusCode);
        using var scope = host.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().SupportTickets.IgnoreQueryFilters().CountAsync());
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public bool ObservedSavedRows { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken cancellationToken = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<AuditEntry>().Any(x => x.Entity.Action.StartsWith("support.")))
            {
                ObservedSavedRows = data.Context.ChangeTracker.Entries<SupportTicket>().Any(x => x.Entity.Id > 0 && x.State == EntityState.Unchanged);
                throw new InvalidOperationException("TestAuditCompletionFailure");
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class FailingNotifier : ISupportNotifier
    { public Task ChangedAsync(int ticketId, bool internalOnly, CancellationToken ct) => throw new InvalidOperationException("TestNotificationFailure"); }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OverlappingTrackedReads_ConflictAtSave_AndRollBackLosingMessage(bool closeWins)
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(client); var id = ticket.GetProperty("id").GetInt32(); var version = ticket.GetProperty("rowVersion").GetInt64();
        using var sendScope = factory.Services.CreateScope(); using var closeScope = factory.Services.CreateScope();
        var teacher = new Principal(ticket.GetProperty("authorId").GetInt32(), factory.InstitutionA, "Teacher");
        var directory = closeScope.ServiceProvider.GetRequiredService<IPlatformAdminDirectory>();
        var master = (await directory.ListAsync(1, 100, default)).Items.Single(x => x.IsMaster);
        var admin = new Principal(master.Id, null, "PlatformAdmin");
        var sender = Service(sendScope.ServiceProvider, teacher);
        var closer = Service(closeScope.ServiceProvider, admin);
        // Ambas as unidades carregam a mesma versão antes de qualquer commit.
        await sendScope.ServiceProvider.GetRequiredService<ISupportStore>().FindTicketAsync(await Authorization(sendScope.ServiceProvider, teacher).RequireAsync("reply", id, default), id, default);
        await closeScope.ServiceProvider.GetRequiredService<ISupportStore>().FindTicketAsync(await Authorization(closeScope.ServiceProvider, admin).RequireAsync("manage", id, default), id, default);
        Func<Task> send = async () => await sender.SendAsync(id, new(Guid.NewGuid(), version, "Resposta concorrente", false, []), default);
        Func<Task> close = async () => await closer.CloseAsync(id, new(version, "Resolvido"), default);
        await (closeWins ? close() : send());
        var failure = await Assert.ThrowsAsync<SupportException>(closeWins ? send : close);
        Assert.Equal("VersionConflict", failure.Code);
        using var verify = factory.Services.CreateScope(); var db = verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Equal(closeWins ? 1 : 2, await db.SupportMessages.IgnoreQueryFilters().CountAsync());
        Assert.Equal(closeWins ? SupportStatus.Closed : SupportStatus.Open, (await db.SupportTickets.IgnoreQueryFilters().SingleAsync()).Status);
        Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.Action.StartsWith("support.")));
    }

    [Fact]
    public async Task DirectEfWrites_CannotModifyAnySupportAggregate_AndDefaultFiltersHideOtherTenants()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(client);
        using var scope = factory.Services.CreateScope(); var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SchoolGetherDbContext>>();
        await using var otherTenant = new SchoolGetherDbContext(options, new Principal(999, factory.InstitutionB, "InstitutionAdmin"));
        Assert.Empty(await otherTenant.SupportTickets.ToListAsync()); Assert.Empty(await otherTenant.SupportMessages.ToListAsync());
        var stolen = await otherTenant.SupportTickets.IgnoreQueryFilters().SingleAsync();
        stolen.ChangePriority(SupportPriority.High, DateTime.UtcNow);
        Assert.Equal("SupportWriteDenied", (await Assert.ThrowsAsync<InvalidOperationException>(() => otherTenant.SaveChangesAsync())).Message);
        otherTenant.ChangeTracker.Clear();
        otherTenant.SupportTickets.Add(SupportTicket.Open(factory.InstitutionA, 999, "Forjado", Guid.NewGuid(), "Forjado", SupportCategory.Other, SupportPriority.Normal, DateTime.UtcNow));
        Assert.Equal("SupportWriteDenied", Assert.Throws<InvalidOperationException>(() => otherTenant.SaveChanges()).Message);
        await using var teacher = new SchoolGetherDbContext(options, new Principal(ticket.GetProperty("authorId").GetInt32(), factory.InstitutionA, "Teacher"));
        Assert.Single(await teacher.SupportTickets.ToListAsync());
        await using var colleague = new SchoolGetherDbContext(options, new Principal(999, factory.InstitutionA, "Teacher"));
        Assert.Empty(await colleague.SupportTickets.ToListAsync()); Assert.Empty(await colleague.SupportMessages.ToListAsync());
    }

    private static SupportAuthorization Authorization(IServiceProvider services, Principal actor) => new(actor,
        services.GetRequiredService<IIdentityStore>(), new(actor, services.GetRequiredService<IPlatformAdminDirectory>(), services.GetRequiredService<IAuditWriter>()),
        services.GetRequiredService<IPlatformAdminDirectory>(), new() { Enabled = true });
    private static SupportService Service(IServiceProvider services, Principal actor) => new(Authorization(services, actor),
        services.GetRequiredService<ISupportStore>(), services.GetRequiredService<ISupportUnitOfWork>(), services.GetRequiredService<IAuditWriter>(),
        services.GetRequiredService<IPlatformAdminDirectory>(), TimeProvider.System, services.GetRequiredService<ISupportNotifier>());
    private sealed record Principal(int? UserId, int? InstitutionId, string? Role) : ICurrentAccount;
    [Fact]
    public async Task SendAndClose_CompeteForSameVersion_OnlyOneWins()
    {
        await using var factory = new IdentityFactory(supportEnabled: true); await factory.SeedAsync();
        using var customer = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(customer, "teacher-a@example.test");
        using var admin = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(admin);
        var ticket = await SupportAuthorizationTests.Ticket(customer); var id = ticket.GetProperty("id").GetInt32(); var version = ticket.GetProperty("rowVersion").GetInt64();
        var send = await customer.PostAsJsonAsync($"{SupportAuthorizationTests.Route}/{id}/messages", new { ClientId = Guid.NewGuid(), RowVersion = version, Text = "Corrida", Internal = false });
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);
        var close = await admin.PostAsJsonAsync($"{SupportAuthorizationTests.Route}/{id}/close", new { RowVersion = version, Summary = "Resolvido" });
        Assert.Equal(HttpStatusCode.Conflict, close.StatusCode);
    }

    [Fact]
    public async Task AuditFailure_RollsBackTicketAndMessage()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, auditWriter: new FailingAudit()); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync(SupportAuthorizationTests.Route, SupportAuthorizationTests.Create())).StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>(SupportAuthorizationTests.Route);
        Assert.Equal(0, list.GetProperty("total").GetInt32());
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Append(AuditEvent auditEvent) => throw new InvalidOperationException("TestAuditFailure");
        public Task AppendIndependentAsync(AuditEvent auditEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
