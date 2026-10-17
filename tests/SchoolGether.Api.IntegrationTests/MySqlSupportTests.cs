using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class MySqlSupportTests
{
    [MySqlFact]
    public async Task UpgradeFromSixMigrations_PreservesLegacyData_IsolatesSupport_AndSerializesWrites()
    {
        await using var factory = new IdentityFactory(
            mySqlConnectionString: Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"),
            migrationTarget: "20261014210542_InstitutionSetupCompletion", onboardingEnabled: true, supportEnabled: true);
        await factory.SeedAsync();
        using var master = factory.CreateClient(); await PlatformAdminTests.Authenticate(master);
        using var layout = await master.PutAsJsonAsync(DashboardPreferenceTests.Root, DashboardPreferenceTests.Layout(0));
        layout.EnsureSuccessStatusCode();
        using var anonymous = factory.CreateClient();
        using var signup = await anonymous.PostAsJsonAsync(OnboardingTests.Route, OnboardingTests.Request("support-legacy@example.test"));
        signup.EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            Assert.Equal(6, (await db.Database.GetAppliedMigrationsAsync()).Count());
            var users = await db.Users.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
            var members = await db.Members.IgnoreQueryFilters().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
            var audits = await db.AuditEntries.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
            var subscription = await db.Subscriptions.IgnoreQueryFilters().AsNoTracking().SingleAsync();
            var subscriptionJson = JsonSerializer.Serialize(subscription);
            await db.Database.MigrateAsync();
            Assert.Equal("MySql.EntityFrameworkCore", db.Database.ProviderName);
            Assert.Equal(8, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(users, await db.Users.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
            Assert.Equal(members, await db.Members.IgnoreQueryFilters().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
            Assert.Equal(audits, await db.AuditEntries.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
            Assert.Equal(subscriptionJson, JsonSerializer.Serialize(await db.Subscriptions.IgnoreQueryFilters().AsNoTracking().SingleAsync()));
        }
        var savedLayout = await master.GetFromJsonAsync<JsonElement>(DashboardPreferenceTests.Root);
        Assert.Equal("institutions-status", savedLayout.GetProperty("widgets")[0].GetProperty("id").GetString());
        using var teacher = factory.CreateClient(); await PlatformAdminTests.Authenticate(teacher, "teacher-a@example.test");
        using var other = factory.CreateClient(); await PlatformAdminTests.Authenticate(other, "admin-b@example.test");
        var clientId = Guid.NewGuid();
        var creates = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => teacher.PostSupportAsync(SupportAuthorizationTests.Route, SupportAuthorizationTests.Create(clientId))));
        Assert.Equal(1, creates.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.All(creates, x => Assert.True(x.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.Conflict));
        var ticket = await creates.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>();
        var id = ticket.GetProperty("id").GetInt32();
        var initialVersion = ticket.GetProperty("rowVersion").GetInt64();
        foreach (var response in creates) response.Dispose();
        using var replay = await teacher.PostSupportAsync(SupportAuthorizationTests.Route, SupportAuthorizationTests.Create(clientId));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(id, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32());
        var route = $"{SupportAuthorizationTests.Route}/{id}";
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(route)).StatusCode);
        await using var live = new SupportLiveTestClient(factory, teacher);
        await live.StartAsync(); await live.JoinAsync(id);
        var hidden = await SupportAuthorizationTests.Ticket(other);
        await Assert.ThrowsAsync<HubException>(() => live.JoinAsync(hidden.GetProperty("id").GetInt32()));
        using var noteBody = SupportAttachmentTests.Multipart(new { ClientId = Guid.NewGuid(), RowVersion = initialVersion, Text = "Nota MySQL reservada", Internal = true }, ("private.txt", "private"u8.ToArray()));
        using var note = await master.PostAsync(route + "/messages", noteBody); note.EnsureSuccessStatusCode();
        var attachmentId = (await note.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attachments")[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/v1/support/attachments/{attachmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/support/attachments/{attachmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await master.GetAsync($"/api/v1/support/attachments/{attachmentId}")).StatusCode);
        Assert.DoesNotContain("Nota MySQL reservada", await teacher.GetStringAsync(route + "/messages"));
        await live.AssertQuietAsync();
        var messageId = Guid.NewGuid();
        var sends = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => teacher.PostSupportAsync(route + "/messages", new { ClientId = messageId, RowVersion = initialVersion + 1, Text = "Envio idempotente MySQL", Internal = false })));
        Assert.Equal(1, sends.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.All(sends, x => Assert.True(x.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.Conflict));
        var persistedMessageId = (await sends.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        foreach (var response in sends) response.Dispose();
        using var messageReplay = await teacher.PostSupportAsync(route + "/messages", new { ClientId = messageId, RowVersion = initialVersion + 1, Text = "Envio idempotente MySQL", Internal = false });
        Assert.Equal(HttpStatusCode.OK, messageReplay.StatusCode);
        Assert.Equal(persistedMessageId, (await messageReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32());
        Assert.Equal(id, (await live.NextAsync()).GetProperty("ticketId").GetInt32());
        await live.AssertQuietAsync();
        // Different ticket aggregates compete for the same institution's remaining bytes.
        factory.Services.GetRequiredService<SchoolGether.Application.Support.SupportOptions>().InstitutionStorageLimitBytes = 13;
        var second = await SupportAuthorizationTests.Ticket(teacher);
        async Task<HttpStatusCode> Attach(int ticketId, long version)
        {
            using var body = SupportAttachmentTests.Multipart(new { ClientId = Guid.NewGuid(), RowVersion = version, Text = "", Internal = false }, ("quota.txt", "four"u8.ToArray()));
            using var response = await teacher.PostAsync($"{SupportAuthorizationTests.Route}/{ticketId}/messages", body);
            return response.StatusCode;
        }
        var quota = await Task.WhenAll(Task.Run(() => Attach(id, initialVersion + 2)), Task.Run(() => Attach(second.GetProperty("id").GetInt32(), second.GetProperty("rowVersion").GetInt64())));
        Assert.Equal(1, quota.Count(x => x == HttpStatusCode.Created)); Assert.Equal(1, quota.Count(x => x == HttpStatusCode.Conflict));
        factory.Services.GetRequiredService<SchoolGether.Application.Support.SupportOptions>().InstitutionStorageLimitBytes = 100 * 1024 * 1024;
        var latest = await teacher.GetFromJsonAsync<JsonElement>(route);
        var race = await Task.WhenAll(teacher.PostSupportAsync(route + "/messages", new { ClientId = Guid.NewGuid(), RowVersion = latest.GetProperty("rowVersion").GetInt64(), Text = "Corrida envio fecho", Internal = false }),
            teacher.PostSupportAsync(route + "/close", new { RowVersion = latest.GetProperty("rowVersion").GetInt64(), Summary = "Ensaio MySQL concluído" }));
        Assert.Equal(1, race.Count(x => x.IsSuccessStatusCode)); Assert.Equal(1, race.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        var sendWon = race[0].IsSuccessStatusCode;
        foreach (var response in race) response.Dispose();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            Assert.Single(await db.SupportTickets.IgnoreQueryFilters().Where(x => x.ClientId == clientId).ToListAsync());
            Assert.Single(await db.SupportMessages.IgnoreQueryFilters().Where(x => x.ClientId == messageId).ToListAsync());
            Assert.Equal(11, (await db.SupportStorageUsages.IgnoreQueryFilters().SingleAsync()).UsedBytes);
            Assert.Equal(2, await db.SupportAttachments.IgnoreQueryFilters().CountAsync());
            Assert.Equal(6 + (sendWon ? 1 : 0), await db.SupportMessages.IgnoreQueryFilters().CountAsync());
            Assert.Equal(sendWon ? "Open" : "Closed", (await db.SupportTickets.IgnoreQueryFilters().SingleAsync(x => x.Id == id)).Status.ToString());
        }
        // Both school closing scopes must pass the Oracle provider's property/audit write guards.
        using var schoolAdmin = factory.CreateClient(); await PlatformAdminTests.Authenticate(schoolAdmin, "admin-a@example.test");
        var secondId = second.GetProperty("id").GetInt32();
        var secondLatest = await schoolAdmin.GetFromJsonAsync<JsonElement>($"{SupportAuthorizationTests.Route}/{secondId}");
        using var schoolClose = await schoolAdmin.PostAsJsonAsync($"{SupportAuthorizationTests.Route}/{secondId}/close", new {
            RowVersion = secondLatest.GetProperty("rowVersion").GetInt64(), Summary = "Fecho pelo responsável escolar" });
        Assert.Equal(HttpStatusCode.OK, schoolClose.StatusCode);
        latest = await teacher.GetFromJsonAsync<JsonElement>(route);
        if (sendWon)
        {
            using var teacherClose = await teacher.PostAsJsonAsync(route + "/close", new { RowVersion = latest.GetProperty("rowVersion").GetInt64(), Summary = "Fecho pelo docente" });
            Assert.Equal(HttpStatusCode.OK, teacherClose.StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(route + "/close", new { RowVersion = 1, Summary = "Fora do âmbito" })).StatusCode);
        using (var verify = factory.Services.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            Assert.Equal(2, await db.AuditEntries.CountAsync(x => x.Action == "support.close"));
            Assert.Single(await db.AuditEntries.Where(x => x.Action == "support.download").ToListAsync());
            Assert.Equal(2, await db.SupportTickets.IgnoreQueryFilters().CountAsync(x => x.Status == SchoolGether.Domain.Support.SupportStatus.Closed));
        }
        // Optional local browser rehearsal uses actual Kestrel/MySQL, never intercepted API responses.
        var browserScript = Environment.GetEnvironmentVariable("SCHOOLGETHER_SUPPORT_BROWSER_SCRIPT");
        if (!string.IsNullOrEmpty(browserScript))
        {
            Assert.True(Environment.Version >= new Version(10, 0, 13));
            var aspNetVersion = FileVersionInfo.GetVersionInfo(typeof(Hub).Assembly.Location).ProductVersion!.Split('+')[0];
            Assert.True(Version.Parse(aspNetVersion) >= new Version(10, 0, 13));
            await using var kestrel = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "http://localhost:5361" })));
            kestrel.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
            using var actual = kestrel.CreateClient();
            var serverAddress = kestrel.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("SCHOOLGETHER_NODE")!, browserScript) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment["SUPPORT_API_URL"] = serverAddress.TrimEnd('/') + "/";
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try { await process.WaitForExitAsync(timeout.Token); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            File.WriteAllText(Path.ChangeExtension(browserScript, ".log"), await output + await error);
            Assert.Equal(0, process.ExitCode);
            using var verify = factory.Services.CreateScope();
            var db = verify.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            var browserTicket = await db.SupportTickets.IgnoreQueryFilters().SingleAsync(x => x.Subject.StartsWith("Browser MySQL real "));
            Assert.Equal("Closed", browserTicket.Status.ToString());
            Assert.Equal("Fecho real durante interrupção do cliente.", browserTicket.CloseSummary);
            Assert.Single(await db.SupportMessages.IgnoreQueryFilters().Where(x => x.TicketId == browserTicket.Id && x.Internal).ToListAsync());
            Assert.Equal(4, await db.SupportAttachments.IgnoreQueryFilters().CountAsync());
            File.WriteAllText(Path.ChangeExtension(browserScript, ".server-proof.json"), JsonSerializer.Serialize(new {
                Runtime = Environment.Version.ToString(), AspNetRuntime = aspNetVersion, ApiAddress = serverAddress,
                DatabaseProvider = db.Database.ProviderName, BrowserTicketId = browserTicket.Id, Status = browserTicket.Status.ToString()
            }));
        }
        var evidence = Environment.GetEnvironmentVariable("SCHOOLGETHER_SUPPORT_BACKUP_FILES");
        if (!string.IsNullOrEmpty(evidence))
        {
            Directory.CreateDirectory(evidence);
            var filesRoot = factory.Services.GetRequiredService<IConfiguration>()["Support:FilesRoot"]!;
            foreach (var file in Directory.EnumerateFiles(Path.Combine(filesRoot, "files")))
                File.Copy(file, Path.Combine(evidence, Path.GetFileName(file)), overwrite: false);
        }
    }
}
