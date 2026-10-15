using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Contracts.Identity;
using SchoolGether.Contracts.Institutions;
using SchoolGether.Infrastructure.Persistence;
using SchoolGether.Contracts.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchoolGether.Infrastructure.Identity;
using SchoolGether.Domain.Audit;

namespace SchoolGether.Api.IntegrationTests;

public sealed class MySqlIdentityTests
{
    [MySqlFact]
    public async Task Identity_WithMySqlMigrations_IsolatesInstitutionsAndRevokesSessions()
    {
        await using var factory = new IdentityFactory(
            mySqlConnectionString: Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"));
        await factory.SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            Assert.Equal("MySql.EntityFrameworkCore", db.Database.ProviderName);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        Assert.NotEqual(factory.InstitutionA, factory.InstitutionB);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var platform = await Login(client, "platform@example.test");
        Authenticate(client, platform);
        using var createInstitution = await client.PostAsJsonAsync("/api/v1/institutions", new CreateInstitutionRequest("Instituição C"));
        Assert.Equal(HttpStatusCode.Created, createInstitution.StatusCode);
        var institution = (await createInstitution.Content.ReadFromJsonAsync<InstitutionResponse>())!;
        using var createAdmin = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "admin-c@example.test", IdentityFactory.Password, "Admin C", "InstitutionAdmin", institution.Id));
        Assert.Equal(HttpStatusCode.Created, createAdmin.StatusCode);

        var adminC = await Login(client, "admin-c@example.test");
        Authenticate(client, adminC);
        var me = (await client.GetFromJsonAsync<AccountResponse>("/api/v1/identity/me"))!;
        Assert.Equal("InstitutionAdmin", me.Role);
        Assert.Equal(institution.Id, me.InstitutionId);
        using var createStudent = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "student-c@example.test", IdentityFactory.Password, "Aluno C", "Student", null));
        Assert.Equal(HttpStatusCode.Created, createStudent.StatusCode);
        var membersC = (await client.GetFromJsonAsync<MemberPageResponse>("/api/v1/identity/users"))!;
        Assert.Equal(2, membersC.Total);
        Assert.All(membersC.Items, member => Assert.Equal(institution.Id, member.InstitutionId));
        Assert.Contains(membersC.Items, member => member.Email == "student-c@example.test");

        using var crossInstitution = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "blocked@example.test", IdentityFactory.Password, "Bloqueado", "Student", factory.InstitutionA));
        Assert.Equal(HttpStatusCode.Forbidden, crossInstitution.StatusCode);

        var adminA = await Login(client, "admin-a@example.test");
        Authenticate(client, adminA);
        var membersA = (await client.GetFromJsonAsync<MemberPageResponse>("/api/v1/identity/users"))!;
        Assert.Equal(3, membersA.Total);
        Assert.All(membersA.Items, member => Assert.Equal(factory.InstitutionA, member.InstitutionId));
        Assert.DoesNotContain(membersA.Items, member => member.Email == "blocked@example.test");

        foreach (var email in new[] { "teacher-a@example.test", "student-c@example.test" })
        {
            var token = await Login(client, email);
            Authenticate(client, token);
            Assert.Equal(token.Account, await client.GetFromJsonAsync<AccountResponse>("/api/v1/identity/me"));
            using var forbidden = await client.GetAsync("/api/v1/identity/users");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        using var refresh = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(adminA.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var next = (await refresh.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(adminA.RefreshToken, next.RefreshToken);
        using var logout = await client.PostAsJsonAsync("/api/v1/identity/logout", new RefreshRequest(next.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var revoked = await client.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(next.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);

        // Mesmo datadir e migrações; novo host reinicia apenas o limitador HTTP de teste.
        // Não prepara outra fixture nem volta a semear a base.
        var auditFailure = new FailPlatformAudit();
        await using var platformHost = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<SchoolGetherDbContext>(options => options.AddInterceptors(auditFailure))));
        using var global = platformHost.CreateClient();
        var master = await Login(global, "platform@example.test"); Authenticate(global, master);
        var listing = (await global.GetFromJsonAsync<PagedResponse<PlatformInstitutionResponse>>(PlatformInstitutionTests.Root))!;
        Assert.Equal(3, listing.Total);
        Assert.Equal(3, listing.Items.Single(x => x.Id == factory.InstitutionA).AccountsCount);
        var detail = (await global.GetFromJsonAsync<PlatformInstitutionDetailResponse>($"{PlatformInstitutionTests.Root}/{factory.InstitutionB}"))!;
        Assert.Single(detail.Administrators); Assert.Equal(factory.InstitutionB, detail.Administrators[0].InstitutionId);
        using var createGlobal = await global.PostAsJsonAsync(PlatformInstitutionTests.Root, new CreatePlatformInstitutionRequest { Name = "Politécnico Ção", Reason = "Adesão" });
        Assert.Equal(HttpStatusCode.Created, createGlobal.StatusCode);
        var newInstitution = (await createGlobal.Content.ReadFromJsonAsync<PlatformInstitutionResponse>())!;
        using var rename = await global.PutAsJsonAsync($"{PlatformInstitutionTests.Root}/{newInstitution.Id}", new UpdatePlatformInstitutionRequest {
            Name = "Politécnico Ção atualizado", RowVersion = 1, Reason = "Correção" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        using var staleInstitution = await global.PutAsJsonAsync($"{PlatformInstitutionTests.Root}/{newInstitution.Id}", new UpdatePlatformInstitutionRequest {
            Name = "Versão antiga", RowVersion = 1, Reason = "Correção" });
        Assert.Equal(HttpStatusCode.Conflict, staleInstitution.StatusCode);
        using var responsible = await global.PostAsJsonAsync($"{PlatformInstitutionTests.Root}/{newInstitution.Id}/administrators", new CreateInstitutionAdminRequest {
            Email = "platform-responsible@example.test", Password = IdentityFactory.Password, Name = "Responsável", Reason = "Designação" });
        Assert.Equal(HttpStatusCode.Created, responsible.StatusCode);
        Assert.Equal(newInstitution.Id, (await responsible.Content.ReadFromJsonAsync<AccountResponse>())!.InstitutionId);
        var unicode = (await global.GetFromJsonAsync<PagedResponse<PlatformInstitutionResponse>>(PlatformInstitutionTests.Root + "?search=" + Uri.EscapeDataString("POLITÉCNICO ÇÃO")))!;
        Assert.Equal(1, unicode.Total);

        var firstWrites = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => global.PutAsJsonAsync(DashboardPreferenceTests.Root, DashboardPreferenceTests.Layout(0))));
        Assert.Equal(1, firstWrites.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, firstWrites.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in firstWrites) response.Dispose();
        var competingUpdates = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => global.PutAsJsonAsync(DashboardPreferenceTests.Root, DashboardPreferenceTests.Layout(1, "institutions-trend"))));
        Assert.Equal(1, competingUpdates.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, competingUpdates.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in competingUpdates) response.Dispose();
        var persisted = (await global.GetFromJsonAsync<DashboardPreferenceResponse>(DashboardPreferenceTests.Root))!;
        Assert.Equal(2, persisted.RowVersion); Assert.Equal("institutions-trend", Assert.Single(persisted.Widgets).Id);
        var colleague = await PlatformAdminTests.Created(global, "mysql-colleague@example.test", "institutions.read");
        using var colleagueClient = platformHost.CreateClient();
        var colleagueToken = await Login(colleagueClient, "mysql-colleague@example.test"); Authenticate(colleagueClient, colleagueToken);
        using var colleagueSave = await colleagueClient.PutAsJsonAsync(DashboardPreferenceTests.Root, DashboardPreferenceTests.Layout(0));
        Assert.Equal(HttpStatusCode.OK, colleagueSave.StatusCode);
        Assert.Equal("institutions-status", Assert.Single((await colleagueClient.GetFromJsonAsync<DashboardPreferenceResponse>(DashboardPreferenceTests.Root))!.Widgets).Id);
        Assert.Equal("institutions-trend", Assert.Single((await global.GetFromJsonAsync<DashboardPreferenceResponse>(DashboardPreferenceTests.Root))!.Widgets).Id);
        using var revokeRead = await global.PutAsJsonAsync("/api/v1/platform/admins/" + colleague.GetProperty("id").GetInt32(), new {
            Name = "Revogado", IsActive = true, Permissions = Array.Empty<string>(), RowVersion = 1, Reason = "Revogação" });
        Assert.Equal(HttpStatusCode.OK, revokeRead.StatusCode);
        var noData = (await colleagueClient.GetFromJsonAsync<PlatformDashboardResponse>("/api/v1/platform/dashboard"))!;
        Assert.Null(noData.InstitutionsTotal); Assert.Null(noData.AccountsTotal); Assert.Empty(noData.InstitutionsTrend); Assert.Empty(noData.RecentActions);
        Assert.Empty((await colleagueClient.GetFromJsonAsync<DashboardPreferenceResponse>(DashboardPreferenceTests.Root))!.Widgets);

        auditFailure.Enabled = true;
        using var rollbackSuspension = await global.PutAsJsonAsync($"{PlatformInstitutionTests.Root}/{factory.InstitutionA}/suspension", new SetInstitutionSuspensionRequest {
            IsSuspended = true, RowVersion = 1, Reason = "Não persistir" });
        Assert.Equal(HttpStatusCode.InternalServerError, rollbackSuspension.StatusCode);
        using var rollbackAdministrator = await global.PostAsJsonAsync($"{PlatformInstitutionTests.Root}/{factory.InstitutionA}/administrators", new CreateInstitutionAdminRequest {
            Email = "mysql-rollback@example.test", Password = IdentityFactory.Password, Name = "Não persistir", Reason = "Rollback" });
        Assert.Equal(HttpStatusCode.InternalServerError, rollbackAdministrator.StatusCode);
        auditFailure.Enabled = false;
        using (var scope = platformHost.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            Assert.False((await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionA)).IsSuspended);
            Assert.False(await db.Users.AnyAsync(x => x.Email == "mysql-rollback@example.test"));
            Assert.False(await db.AuditEntries.AnyAsync(x => x.Reason == "Não persistir"));
            Assert.Single(await db.Users.Where(x => x.MasterSlot == 1).ToListAsync());
        }

        using var school = platformHost.CreateClient();
        var oldSchool = await Login(school, "admin-a@example.test"); Authenticate(school, oldSchool);
        using var suspended = await global.PutAsJsonAsync($"{PlatformInstitutionTests.Root}/{factory.InstitutionA}/suspension", new SetInstitutionSuspensionRequest {
            IsSuspended = true, RowVersion = 1, Reason = "Investigação" });
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        using var oldJwt = await school.GetAsync("/api/v1/identity/users"); Assert.Equal(HttpStatusCode.Unauthorized, oldJwt.StatusCode);
        using var blockedLogin = await school.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest("admin-a@example.test", IdentityFactory.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, blockedLogin.StatusCode);
        using var blockedRefresh = await school.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(oldSchool.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, blockedRefresh.StatusCode);
        using (var scope = platformHost.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var lockedAccount = (await users.FindByEmailAsync("admin-a@example.test"))!;
            Assert.True((await users.SetLockoutEndDateAsync(lockedAccount, DateTimeOffset.UtcNow.AddDays(1))).Succeeded);
        }
        using var reactivated = await global.PutAsJsonAsync($"{PlatformInstitutionTests.Root}/{factory.InstitutionA}/suspension", new SetInstitutionSuspensionRequest {
            IsSuspended = false, RowVersion = 2, Reason = "Reabertura" });
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        using var stillLocked = await school.GetAsync("/api/v1/identity/me"); Assert.Equal(HttpStatusCode.Unauthorized, stillLocked.StatusCode);
        var eligible = await Login(school, "student-a@example.test"); Authenticate(school, eligible);
        using var studentMe = await school.GetAsync("/api/v1/identity/me"); Assert.Equal(HttpStatusCode.OK, studentMe.StatusCode);
        var dashboard = (await global.GetFromJsonAsync<PlatformDashboardResponse>("/api/v1/platform/dashboard"))!;
        Assert.Equal(4, dashboard.InstitutionsTotal); Assert.Equal(6, dashboard.InstitutionsTrend.Count);
        Assert.Contains("parcial", dashboard.InstitutionsTrend[^1].Label);
        var institutionHistory = (await global.GetFromJsonAsync<PagedResponse<AuditResponse>>($"/api/v1/platform/audit?institutionId={factory.InstitutionA}&search=institution.suspension&pageSize=1"))!;
        Assert.Equal(2, institutionHistory.Total); Assert.All(institutionHistory.Items, x => Assert.Equal(factory.InstitutionA, x.InstitutionId));
    }

    [MySqlFact]
    public async Task Setup_upgrade_preserves_legacy_data_and_serializes_concurrent_edits()
    {
        // The runner supplies its own loopback instance; seed the schema deployed before onboarding.
        await using var factory = new IdentityFactory(
            mySqlConnectionString: Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"),
            migrationTarget: "20261012213240_PlatformInstitutionsAndDashboard");
        await factory.SeedAsync();
        using var global = factory.CreateClient();
        Authenticate(global, await Login(global, "platform@example.test"));
        using var suspension = await global.PutAsJsonAsync($"/api/v1/platform/institutions/{factory.InstitutionB}/suspension",
            new { isSuspended = true, rowVersion = 1, reason = "Suspensão anterior à migração" });
        Assert.Equal(HttpStatusCode.OK, suspension.StatusCode);
        using var layout = await global.PutAsJsonAsync(DashboardPreferenceTests.Root, DashboardPreferenceTests.Layout(0));
        Assert.Equal(HttpStatusCode.OK, layout.StatusCode);
        int[] legacyUsers;
        int[] legacyMembers;
        int[] legacyAudits;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            legacyUsers = await db.Users.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
            legacyMembers = await db.Members.IgnoreQueryFilters().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
            legacyAudits = await db.AuditEntries.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
            Assert.NotEmpty(legacyAudits);
            Assert.Equal("20261012213240_PlatformInstitutionsAndDashboard", (await db.Database.GetAppliedMigrationsAsync()).Last());
            await db.Database.MigrateAsync();
            Assert.Equal("MySql.EntityFrameworkCore", db.Database.ProviderName);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal("20261014210542_InstitutionSetupCompletion", (await db.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal(legacyUsers, await db.Users.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
            Assert.Equal(legacyMembers, await db.Members.IgnoreQueryFilters().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
            Assert.Equal(legacyAudits, await db.AuditEntries.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
            Assert.True((await db.Institutions.AsNoTracking().SingleAsync(x => x.Id == factory.InstitutionB)).IsSuspended);
            Assert.Equal(2, await db.Institutions.CountAsync());
            Assert.Empty(await db.InstitutionProfiles.IgnoreQueryFilters().ToListAsync());
            Assert.Empty(await db.Subscriptions.IgnoreQueryFilters().ToListAsync());
        }
        var preservedLayout = (await global.GetFromJsonAsync<DashboardPreferenceResponse>(DashboardPreferenceTests.Root))!;
        Assert.Equal(1, preservedLayout.RowVersion);
        Assert.Equal("institutions-status", Assert.Single(preservedLayout.Widgets).Id);
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        Authenticate(first, await Login(first, "admin-a@example.test"));
        Authenticate(second, await Login(second, "admin-a@example.test"));
        const string setupRoute = "/api/v1/institutions/current/setup";
        var initial = (await first.GetFromJsonAsync<InstitutionSetupResponse>(setupRoute))!;
        Assert.False(initial.IsComplete);
        Assert.Equal(1, initial.RowVersion);
        var setupWrites = await Task.WhenAll(first.PutAsJsonAsync(setupRoute, Setup(1, "Primeiro rascunho")),
            second.PutAsJsonAsync(setupRoute, Setup(1, "Segundo rascunho")));
        AssertOneWinner(setupWrites);
        var completed = (await first.GetFromJsonAsync<InstitutionSetupResponse>(setupRoute))!;
        Assert.True(completed.IsComplete);
        Assert.Equal(2, completed.RowVersion);
        var winnerIndex = Array.FindIndex(setupWrites, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Equal(winnerIndex == 0 ? "Primeiro rascunho" : "Segundo rascunho", completed.Name);
        foreach (var response in setupWrites) response.Dispose();
        DateTime? completion;
        using (var scope = factory.Services.CreateScope())
        {
            var profile = await scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>()
                .InstitutionProfiles.IgnoreQueryFilters().SingleAsync();
            completion = profile.CompletedAt;
            Assert.NotNull(completion);
        }
        // Both modules edit the same institution version: one must lose without replacing the winner.
        var competing = await Task.WhenAll(global.PutAsJsonAsync($"/api/v1/platform/institutions/{factory.InstitutionA}",
            new { name = "Nome global concorrente", rowVersion = 2, reason = "Correção concorrente" }),
            first.PutAsJsonAsync(setupRoute, Setup(2, "Configuração concorrente")));
        AssertOneWinner(competing);
        var afterRace = (await first.GetFromJsonAsync<InstitutionSetupResponse>(setupRoute))!;
        Assert.Equal(competing[0].StatusCode == HttpStatusCode.OK ? "Nome global concorrente" : "Configuração concorrente", afterRace.Name);
        Assert.Equal(3, afterRace.RowVersion);
        foreach (var response in competing) response.Dispose();
        using var edited = await first.PutAsJsonAsync(setupRoute, Setup(3, afterRace.Name));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            Assert.Equal(completion, (await db.InstitutionProfiles.IgnoreQueryFilters().SingleAsync()).CompletedAt);
            Assert.Equal(legacyUsers, await db.Users.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
            Assert.Equal(legacyMembers, await db.Members.IgnoreQueryFilters().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
            Assert.All(legacyAudits, id => Assert.True(db.AuditEntries.Any(x => x.Id == id)));
        }
        static SaveInstitutionSetupRequest Setup(long version, string name) => new(name, "Faculty", "Porto", "2026/2027", version);
        static void AssertOneWinner(HttpResponseMessage[] responses)
        {
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        }
    }

    private sealed class FailPlatformAudit : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<AuditEntry>().Any(x => x.State == EntityState.Added))
                throw new InvalidOperationException("Falha de auditoria injetada.");
            return base.SavingChangesAsync(data, result, cancellationToken);
        }
    }

    private static async Task<TokenResponse> Login(HttpClient client, string email)
    {
        client.DefaultRequestHeaders.Authorization = null;
        using var response = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest(email, IdentityFactory.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private static void Authenticate(HttpClient client, TokenResponse token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
}

public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL")))
            Skip = "Executar scripts/Test-MySqlIdentity.ps1 para preparar o MySQL temporário.";
    }
}
