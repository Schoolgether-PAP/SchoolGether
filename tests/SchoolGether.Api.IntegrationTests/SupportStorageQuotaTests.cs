using SchoolGether.Application.Support;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Platform;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Audit;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class SupportStorageQuotaTests
{
    [Fact]
    public async Task QuotaIsInstitutionWide_AndFailedReservationLeavesNoPartialMessage()
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Support:InstitutionStorageLimitBytes"] = "6" })));
        using var client = host.CreateClient(); await factory.SeedAsync(); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        using var first = SupportAttachmentTests.Multipart(SupportAuthorizationTests.Create(), ("one.txt", "four"u8.ToArray()));
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(SupportAuthorizationTests.Route, first)).StatusCode);
        using var second = SupportAttachmentTests.Multipart(SupportAuthorizationTests.Create(), ("two.txt", "four"u8.ToArray()));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(SupportAuthorizationTests.Route, second)).StatusCode);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Equal(1, await db.SupportTickets.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.SupportMessages.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task AuditFailureAfterPreparation_RemovesFilesAndRollsBackQuota()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, auditWriter: new FailingAudit()); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        using var body = SupportAttachmentTests.Multipart(SupportAuthorizationTests.Create(), ("note.txt", "four"u8.ToArray()));
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsync(SupportAuthorizationTests.Route, body)).StatusCode);
        var root = factory.Services.GetRequiredService<IConfiguration>()["Support:FilesRoot"];
        Assert.NotNull(root);
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Empty(await db.SupportMessages.IgnoreQueryFilters().ToListAsync());
    }
    [Fact]
    public async Task ConcurrentReservationsOnDifferentTickets_OnlyOneFits_WithoutTouchingInstitution()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Support:InstitutionStorageLimitBytes"] = "6" })));
        using var a = host.CreateClient(); using var b = host.CreateClient(); await factory.SeedAsync();
        await PlatformAdminTests.Authenticate(a, "teacher-a@example.test"); await PlatformAdminTests.Authenticate(b, "teacher-a@example.test");
        var first = await SupportAuthorizationTests.Ticket(a); var second = await SupportAuthorizationTests.Ticket(b);
        long institutionVersion;
        using (var scope = host.Services.CreateScope()) institutionVersion = (await scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().Institutions.SingleAsync(x => x.Id == factory.InstitutionA)).RowVersion;
        async Task<HttpStatusCode> Send(HttpClient client, JsonElement ticket)
        {
            using var body = SupportAttachmentTests.Multipart(new { ClientId = Guid.NewGuid(), RowVersion = ticket.GetProperty("rowVersion").GetInt64(), Text = "", Internal = false }, ("note.txt", "four"u8.ToArray()));
            return (await client.PostAsync($"{SupportAuthorizationTests.Route}/{ticket.GetProperty("id")}/messages", body)).StatusCode;
        }
        var responses = await Task.WhenAll(Task.Run(() => Send(a, first)), Task.Run(() => Send(b, second)));
        Assert.Equal(1, responses.Count(x => x == HttpStatusCode.Created)); Assert.Equal(1, responses.Count(x => x == HttpStatusCode.Conflict));
        using var verify = host.Services.CreateScope(); var db = verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Equal(4, (await db.SupportStorageUsages.IgnoreQueryFilters().SingleAsync()).UsedBytes);
        Assert.Equal(3, await db.SupportMessages.IgnoreQueryFilters().CountAsync());
        Assert.Single(await db.SupportAttachments.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(institutionVersion, (await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionA)).RowVersion);
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Append(AuditEvent auditEvent) => throw new InvalidOperationException("TestFailureAfterPreparation");
        public Task AppendIndependentAsync(AuditEvent auditEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureAfterFinalization_RollsBackSqlQuotaAndFiles_EvenWhenCanceled(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var failure = new FailAfterFinalization(cancellation, cancel);
        await using var factory = new IdentityFactory(supportEnabled: true);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<SchoolGetherDbContext>(options => options.AddInterceptors(failure))));
        using var client = host.CreateClient(); await factory.SeedAsync();
        using var scope = host.Services.CreateScope(); var services = scope.ServiceProvider;
        var db = services.GetRequiredService<SchoolGetherDbContext>();
        var teacherId = (await db.Users.SingleAsync(x => x.Email == "teacher-a@example.test")).Id;
        var actor = new Principal(teacherId, factory.InstitutionA, "Teacher");
        var directory = services.GetRequiredService<IPlatformAdminDirectory>();
        var audit = services.GetRequiredService<IAuditWriter>();
        var options = services.GetRequiredService<SupportOptions>(); failure.Root = options.FilesRoot;
        var authorization = new SupportAuthorization(actor, services.GetRequiredService<IIdentityStore>(),
            new PlatformAuthorization(actor, directory, audit), directory, options);
        var service = new SupportService(authorization, services.GetRequiredService<ISupportStore>(), services.GetRequiredService<ISupportUnitOfWork>(),
            audit, directory, TimeProvider.System, services.GetRequiredService<ISupportNotifier>(), services.GetRequiredService<IFileStorage>(), options);
        failure.Enabled = true;
        using var bytes = new MemoryStream("saved-before-sql"u8.ToArray());
        var command = new CreateSupportTicket(Guid.NewGuid(), "Teste", "Technical", "Normal", "Descrição", [new("test.txt", bytes)]);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateAsync(command, cancellation.Token));
        else await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(command, cancellation.Token));
        Assert.True(failure.ObservedFinalFileAndSavedRows);
        Assert.Empty(Directory.EnumerateFiles(options.FilesRoot, "*", SearchOption.AllDirectories));
        Assert.Empty(await db.SupportTickets.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.SupportMessages.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.SupportAttachments.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.SupportStorageUsages.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.AuditEntries.Where(x => x.Action.StartsWith("support.")).ToListAsync());
    }
    private sealed record Principal(int? UserId, int? InstitutionId, string? Role) : ICurrentAccount;
    private sealed class FailAfterFinalization(CancellationTokenSource cancellation, bool cancel) : SaveChangesInterceptor
    {
        internal bool Enabled { get; set; }
        internal string Root { get; set; } = "";
        internal bool ObservedFinalFileAndSavedRows { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<SchoolGether.Domain.Support.SupportAttachment>().Any())
            {
                ObservedFinalFileAndSavedRows = Directory.GetFiles(Path.Combine(Root, "files")).Length == 1 &&
                    data.Context.ChangeTracker.Entries<SchoolGether.Domain.Support.SupportAttachment>().All(x => x.Entity.Id > 0);
                if (cancel) { cancellation.Cancel(); ct.ThrowIfCancellationRequested(); }
                throw new InvalidOperationException("TestFailureAfterFinalization");
            }
            return ValueTask.FromResult(result);
        }
    }}
