using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Contracts.Identity;
using SchoolGether.Domain.Audit;
using SchoolGether.Infrastructure.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class PlatformInstitutionTests
{
    internal const string Root = "/api/v1/platform/institutions";
    internal static async Task<JsonElement> Create(HttpClient client, string name = "Politécnico Ção")
    {
        using var response = await client.PostAsJsonAsync(Root, new { Name = name, Reason = "Adesão aprovada" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task ReadOnlyAdmin_SeesBothInstitutionsAndAdministrators_WithoutWriteOrSuspend()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        await PlatformAdminTests.Created(master, "reader@example.test", "institutions.read");
        using var reader = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(reader, "reader@example.test");
        var page = await reader.GetFromJsonAsync<JsonElement>(Root);
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        Assert.Equal(new[] { 3, 2 }, page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("accountsCount").GetInt32()));
        foreach (var id in new[] { factory.InstitutionA, factory.InstitutionB })
        {
            var detail = await reader.GetFromJsonAsync<JsonElement>($"{Root}/{id}");
            Assert.Equal(id, detail.GetProperty("institution").GetProperty("id").GetInt32());
            Assert.Single(detail.GetProperty("administrators").EnumerateArray());
            Assert.All(detail.GetProperty("administrators").EnumerateArray(), x => Assert.Equal(id, x.GetProperty("institutionId").GetInt32()));
        }
        using var denied = await reader.PostAsJsonAsync(Root, new { Name = "Recusada", Reason = "Teste" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var school = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(school, "admin-a@example.test");
        using var forbidden = await school.GetAsync(Root);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Writer_CreatesRenamesAndProvisionsAdministratorInRouteInstitution_ButCannotSuspend()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        await PlatformAdminTests.Created(master, "writer@example.test", "institutions.write");
        using var writer = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(writer, "writer@example.test");
        var created = await Create(writer); var id = created.GetProperty("id").GetInt32();
        using var renamed = await writer.PutAsJsonAsync($"{Root}/{id}", new { Name = "Nova instituição", RowVersion = 1, Reason = "Correção" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal(2, (await renamed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rowVersion").GetInt64());
        using var stale = await writer.PutAsJsonAsync($"{Root}/{id}", new { Name = "Antiga", RowVersion = 1, Reason = "Teste" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var denied = await writer.PutAsJsonAsync($"{Root}/{id}/suspension", new { IsSuspended = true, RowVersion = 2, Reason = "Teste" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var account = await writer.PostAsJsonAsync($"{Root}/{id}/administrators", new {
            Email = "new-admin@example.test", Password = IdentityFactory.Password, Name = "Responsável", Reason = "Primeiro responsável", InstitutionId = factory.InstitutionB });
        Assert.Equal(HttpStatusCode.Created, account.StatusCode);
        var value = await account.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Equal(id, value!.InstitutionId); Assert.Equal("InstitutionAdmin", value.Role);
        using var duplicate = await writer.PostAsJsonAsync($"{Root}/{id}/administrators", new {
            Email = "NEW-ADMIN@example.test", Password = IdentityFactory.Password, Name = "Responsável", Reason = "Duplicado" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var missing = await writer.PostAsJsonAsync($"{Root}/99999/administrators", new {
            Email = "missing@example.test", Password = IdentityFactory.Password, Name = "Responsável", Reason = "Teste" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var history = await master.GetFromJsonAsync<JsonElement>("/api/v1/platform/audit?pageSize=100");
        foreach (var action in new[] { "institution.create", "institution.update", "institution.administrator.create" })
            Assert.Contains(history.GetProperty("items").EnumerateArray(), x => x.GetProperty("action").GetString() == action && x.GetProperty("institutionId").GetInt32() == id);
        Assert.DoesNotContain(IdentityFactory.Password, history.GetRawText());
    }

    [Theory]
    [InlineData("", "Motivo")]
    [InlineData("   ", "Motivo")]
    [InlineData("Escola", "")]
    [InlineData("Escola", "   ")]
    [InlineData("long-name", "Motivo")]
    [InlineData("Escola", "long-reason")]
    public async Task Institutions_RejectInvalidNamesAndReasons(string name, string reason)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client);
        if (name == "long-name") name = new string('a', 201);
        if (reason == "long-reason") reason = new string('a', 501);
        using var create = await client.PostAsJsonAsync(Root, new { Name = name, Reason = reason });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        using var update = await client.PutAsJsonAsync($"{Root}/{factory.InstitutionA}", new { Name = name, Reason = reason, RowVersion = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            using var suspend = await client.PutAsJsonAsync($"{Root}/{factory.InstitutionA}/suspension", new { IsSuspended = true, Reason = reason, RowVersion = 1 });
            Assert.Equal(HttpStatusCode.BadRequest, suspend.StatusCode);
        }
    }

    [Fact]
    public async Task SearchAndFilters_AreAppliedBeforePagination_AndInvalidPagingIsRejected()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client);
        var created = await Create(client); var id = created.GetProperty("id").GetInt32();
        using var suspended = await client.PutAsJsonAsync($"{Root}/{id}/suspension", new { IsSuspended = true, RowVersion = 1, Reason = "Revisão" });
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        var result = await client.GetFromJsonAsync<JsonElement>(Root + "?pageSize=1&isSuspended=true&search=" + Uri.EscapeDataString("ÇÃO"));
        Assert.Equal(1, result.GetProperty("total").GetInt32());
        Assert.Equal(id, result.GetProperty("items")[0].GetProperty("id").GetInt32());
        var byId = await client.GetFromJsonAsync<JsonElement>($"{Root}?search={id}&isSuspended=true");
        Assert.Equal(1, byId.GetProperty("total").GetInt32());
        var active = await client.GetFromJsonAsync<JsonElement>(Root + "?isSuspended=false");
        Assert.Equal(2, active.GetProperty("total").GetInt32());
        foreach (var query in new[] { "page=0", "page=100001", "pageSize=0", "pageSize=101", "search=" + new string('a', 151) })
        {
            using var invalid = await client.GetAsync(Root + "?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var missing = await client.GetAsync(Root + "/99999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Suspension_RejectsOldJwtLoginRefreshAndProvisioning_WithoutAffectingBOrRemovingLockout()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        using var a = PlatformAdminTests.Client(factory);
        using var login = await a.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest("admin-a@example.test", IdentityFactory.Password));
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        a.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        using var b = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(b, "admin-b@example.test");
        using var suspension = await master.PutAsJsonAsync($"{Root}/{factory.InstitutionA}/suspension", new { IsSuspended = true, RowVersion = 1, Reason = "Investigação" });
        Assert.Equal(HttpStatusCode.OK, suspension.StatusCode);
        foreach (var route in new[] { "/api/v1/identity/me", "/api/v1/identity/users" })
        {
            using var denied = await a.GetAsync(route); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        using var newLogin = await a.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest("admin-a@example.test", IdentityFactory.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, newLogin.StatusCode);
        using var refresh = await a.PostAsJsonAsync("/api/v1/identity/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        using var create = await master.PostAsJsonAsync($"{Root}/{factory.InstitutionA}/administrators", new {
            Email = "suspended@example.test", Password = IdentityFactory.Password, Name = "Recusado", Reason = "Teste" });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        var members = (await b.GetFromJsonAsync<MemberPageResponse>("/api/v1/identity/users"))!;
        Assert.Equal(2, members.Total); Assert.All(members.Items, x => Assert.Equal(factory.InstitutionB, x.InstitutionId));
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByEmailAsync("admin-a@example.test"))!;
            Assert.True((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddDays(1))).Succeeded);
        }
        using var reactivation = await master.PutAsJsonAsync($"{Root}/{factory.InstitutionA}/suspension", new { IsSuspended = false, RowVersion = 2, Reason = "Reabertura" });
        Assert.Equal(HttpStatusCode.OK, reactivation.StatusCode);
        using var locked = await a.GetAsync("/api/v1/identity/me"); Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        using var eligible = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(eligible, "student-a@example.test");
        using var profile = await eligible.GetAsync("/api/v1/identity/me"); Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedAudit_RollsBackInstitutionAndAdministrator(bool administrator)
    {
        var failure = new FailInstitutionAudit();
        await using var factory = new IdentityFactory();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<SchoolGetherDbContext>(options => options.AddInterceptors(failure))));
        using var client = host.CreateClient(); await factory.SeedAsync(); await PlatformAdminTests.Authenticate(client);
        failure.Enabled = true;
        using var response = administrator
            ? await client.PostAsJsonAsync($"{Root}/{factory.InstitutionA}/administrators", new { Email = "rollback@example.test", Password = IdentityFactory.Password, Name = "Teste", Reason = "Teste" })
            : await client.PutAsJsonAsync($"{Root}/{factory.InstitutionA}/suspension", new { IsSuspended = true, RowVersion = 1, Reason = "Teste" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.False(await db.Users.AnyAsync(x => x.Email == "rollback@example.test"));
        var institution = await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionA);
        Assert.Equal(1, institution.RowVersion);
        Assert.False(db.Entry(institution).Property<bool>("IsSuspended").CurrentValue);
    }

    [Fact]
    public async Task InstitutionAudit_UsesExactInstitutionFilterBeforePagination_AndRequiresAuditCapability()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        foreach (var id in new[] { factory.InstitutionA, factory.InstitutionB, factory.InstitutionA })
        {
            using var read = await master.GetAsync($"{Root}/{id}"); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }
        var result = await master.GetFromJsonAsync<JsonElement>($"/api/v1/platform/audit?institutionId={factory.InstitutionA}&search=institution.read&pageSize=1");
        Assert.Equal(2, result.GetProperty("total").GetInt32());
        Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal(factory.InstitutionA, result.GetProperty("items")[0].GetProperty("institutionId").GetInt32());
        using var invalid = await master.GetAsync("/api/v1/platform/audit?institutionId=0"); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await PlatformAdminTests.Created(master, "only-institutions@example.test", "institutions.read");
        using var reader = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(reader, "only-institutions@example.test");
        using var denied = await reader.GetAsync($"/api/v1/platform/audit?institutionId={factory.InstitutionA}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Suspension_IsEnforcedByAuthenticationEvenWhenControllerDoesNotLoadProfile()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(SchoolGuardProbeController).Assembly)));
        using var school = host.CreateClient(); await PlatformAdminTests.Authenticate(school, "student-a@example.test");
        using var allowed = await school.GetAsync("/tests/school-guard"); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        using var suspension = await master.PutAsJsonAsync($"{Root}/{factory.InstitutionA}/suspension", new { IsSuspended = true, RowVersion = 1, Reason = "Teste" });
        Assert.Equal(HttpStatusCode.OK, suspension.StatusCode);
        using var denied = await school.GetAsync("/tests/school-guard"); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }
    private sealed class FailInstitutionAudit : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<AuditEntry>().Any(x => x.State == EntityState.Added))
                throw new InvalidOperationException("Falha de auditoria injetada.");
            return base.SavingChangesAsync(data, result, cancellationToken);
        }
    }
}

[Microsoft.AspNetCore.Authorization.Authorize]
[Microsoft.AspNetCore.Mvc.Route("tests/school-guard")]
public sealed class SchoolGuardProbeController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    [Microsoft.AspNetCore.Mvc.HttpGet]
    public Microsoft.AspNetCore.Mvc.IActionResult Get() => Ok();
}
