using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Audit;
using SchoolGether.Contracts.Identity;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class InstitutionSetupTests
{
    private const string Setup = "/api/v1/institutions/current/setup";
    private const string Subscription = "/api/v1/institutions/current/subscription";
    private static object Save(long version = 1, string name = "Escola configurada", string year = "2026/2027", string type = "Faculty", string? locality = "Porto") =>
        new { name, institutionType = type, locality, academicYear = year, rowVersion = version };

    [Fact]
    public async Task Own_institution_setup_is_persisted()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        using var signup = await client.PostAsJsonAsync(OnboardingTests.Route, OnboardingTests.Request());
        Assert.Equal(HttpStatusCode.Created, signup.StatusCode);
        await Login(client, "new-owner@example.test");
        var pending = await Get(client, Setup);
        Assert.False(pending.GetProperty("isComplete").GetBoolean());
        Assert.Equal("School", pending.GetProperty("institutionType").GetString());
        using var saved = await client.PutAsJsonAsync(Setup, Save());
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var actual = await Get(client, Setup);
        Assert.True(actual.GetProperty("isComplete").GetBoolean());
        Assert.Equal("Escola configurada", actual.GetProperty("name").GetString());
        Assert.Equal("Faculty", actual.GetProperty("institutionType").GetString());
        Assert.Equal("Porto", actual.GetProperty("locality").GetString());
        Assert.Equal("2026/2027", actual.GetProperty("academicYear").GetString());
        Assert.Equal(2, actual.GetProperty("rowVersion").GetInt64());
        var completedAt = await Completion(factory);
        Assert.NotNull(completedAt);
        // A same-name edit still advances the shared Institution version and keeps completion time.
        using var edit = await client.PutAsJsonAsync(Setup, Save(2, year: "2027/2028", locality: "Braga"));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(3, (await Get(client, Setup)).GetProperty("rowVersion").GetInt64());
        Assert.Equal(completedAt, await Completion(factory));
        var subscription = await Get(client, Subscription);
        Assert.Equal("Demonstration", subscription.GetProperty("status").GetString());
        Assert.Equal("essential", subscription.GetProperty("plan").GetProperty("id").GetString());
        Assert.Equal(100, subscription.GetProperty("plan").GetProperty("accountLimit").GetInt32());
        Assert.Equal(59m, subscription.GetProperty("plan").GetProperty("monthlyPrice").GetDecimal());
        Assert.Equal("2026-10-14-v1", subscription.GetProperty("catalogVersion").GetString());
        Assert.Equal(1, subscription.GetProperty("usedAccounts").GetInt32());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var audits = await db.AuditEntries.Where(x => x.Action == "institution.setup.complete").ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit => { Assert.Equal("success", audit.Result); Assert.DoesNotContain("new-owner@", audit.After!); Assert.DoesNotContain(IdentityFactory.Password, audit.After!); });
    }

    [Fact]
    public async Task Legacy_missing_profile_has_explicit_pending_state()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = factory.CreateClient(); await Login(client, "admin-a@example.test");
        var pending = await Get(client, Setup);
        Assert.Equal(factory.InstitutionA, pending.GetProperty("institutionId").GetInt32());
        Assert.Equal("Other", pending.GetProperty("institutionType").GetString());
        Assert.False(pending.GetProperty("isComplete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, pending.GetProperty("academicYear").ValueKind);
        Assert.Matches(@"^\d{4}/\d{4}$", pending.GetProperty("suggestedAcademicYear").GetString()!);
        Assert.Equal(1, pending.GetProperty("rowVersion").GetInt64());
        var subscription = await Get(client, Subscription);
        Assert.Equal("None", subscription.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, subscription.GetProperty("plan").ValueKind);
        Assert.Equal(3, subscription.GetProperty("usedAccounts").GetInt32());
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().InstitutionProfiles.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Other_tenant_or_student_cannot_read_or_change_setup()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = factory.CreateClient(); await Login(client, "admin-a@example.test");
        using var saved = await client.PutAsJsonAsync(Setup, Save()); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        await Login(client, "admin-b@example.test");
        var own = await Get(client, Setup + "?institutionId=" + factory.InstitutionA);
        Assert.Equal(factory.InstitutionB, own.GetProperty("institutionId").GetInt32());
        Assert.False(own.GetProperty("isComplete").GetBoolean());
        using var b = await client.PutAsJsonAsync(Setup + "?institutionId=" + factory.InstitutionA, Save(name: "Instituição B atualizada"));
        Assert.Equal(HttpStatusCode.OK, b.StatusCode);
        await Login(client, "admin-a@example.test"); Assert.Equal("Escola configurada", (await Get(client, Setup)).GetProperty("name").GetString());
        foreach (var email in new[] { "student-a@example.test", "teacher-a@example.test", "platform@example.test" })
        {
            await Login(client, email);
            using var read = await client.GetAsync(Setup); Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
            using var write = await client.PutAsJsonAsync(Setup, Save()); Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
            using var subscription = await client.GetAsync(Subscription); Assert.Equal(HttpStatusCode.Forbidden, subscription.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await client.GetAsync(Setup); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Stale_version_preserves_setup()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = factory.CreateClient(); await Login(client, "admin-a@example.test");
        using var first = await client.PutAsJsonAsync(Setup, Save(name: "Instituição A")); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var stale = await client.PutAsJsonAsync(Setup, Save(name: "Rascunho antigo")); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("application/problem+json", stale.Content.Headers.ContentType?.MediaType);
        var result = await Get(client, Setup); Assert.Equal("Instituição A", result.GetProperty("name").GetString()); Assert.Equal(2, result.GetProperty("rowVersion").GetInt64());
    }

    [Fact]
    public async Task Global_rename_conflicts_with_stale_setup()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = factory.CreateClient(); await Login(client, "platform@example.test");
        using var rename = await client.PutAsJsonAsync($"/api/v1/platform/institutions/{factory.InstitutionA}", new { name = "Nome global", rowVersion = 1, reason = "Correção" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        await Login(client, "admin-a@example.test");
        using var stale = await client.PutAsJsonAsync(Setup, Save()); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var result = await Get(client, Setup); Assert.Equal("Nome global", result.GetProperty("name").GetString()); Assert.False(result.GetProperty("isComplete").GetBoolean());
    }

    [Fact]
    public async Task Suspension_rejects_existing_token()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var admin = factory.CreateClient(); await Login(admin, "admin-a@example.test");
        using var global = factory.CreateClient(); await Login(global, "platform@example.test");
        using var suspended = await global.PutAsJsonAsync($"/api/v1/platform/institutions/{factory.InstitutionA}/suspension", new { isSuspended = true, rowVersion = 1, reason = "Suspensão" }); Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        using var read = await admin.GetAsync(Setup); Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        using var write = await admin.PutAsJsonAsync(Setup, Save()); Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
        using var subscription = await admin.GetAsync(Subscription); Assert.Equal(HttpStatusCode.Unauthorized, subscription.StatusCode);
    }

    [Theory]
    [InlineData("2026/2028")][InlineData("2026-2027")][InlineData("1899/1900")][InlineData("2200/2201")][InlineData("2026/2027 ")]
    public async Task Invalid_academic_year_is_rejected_without_changes(string year)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = factory.CreateClient(); await Login(client, "admin-a@example.test");
        using var invalid = await client.PutAsJsonAsync(Setup, Save(year: year)); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.False((await Get(client, Setup)).GetProperty("isComplete").GetBoolean());
    }

    [Theory]
    [InlineData("1900/1901")][InlineData("2199/2200")]
    public async Task Academic_year_boundaries_are_valid(string year)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = factory.CreateClient(); await Login(client, "admin-a@example.test");
        using var valid = await client.PutAsJsonAsync(Setup, Save(year: year)); Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task Audit_failure_rolls_back_name_profile_and_completion()
    {
        await using var factory = new IdentityFactory(auditWriter: new FailingAudit()); await factory.SeedAsync();
        using var client = factory.CreateClient(); await Login(client, "admin-a@example.test");
        using var failure = await client.PutAsJsonAsync(Setup, Save()); Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        var pending = await Get(client, Setup); Assert.False(pending.GetProperty("isComplete").GetBoolean()); Assert.Equal("Instituição A", pending.GetProperty("name").GetString()); Assert.Equal(1, pending.GetProperty("rowVersion").GetInt64());
    }

    [Fact]
    public async Task Setup_and_subscription_document_bearer_authentication()
    {
        await using var factory = new IdentityFactory(); using var client = factory.CreateClient();
        var document = await Get(client, "/openapi/v1.json"); var paths = document.GetProperty("paths");
        foreach (var (route, method) in new[] { (Setup, "get"), (Setup, "put"), (Subscription, "get") })
            Assert.True(paths.GetProperty(route).GetProperty(method).GetProperty("security")[0].TryGetProperty("Bearer", out _));
    }

    [Theory]
    [InlineData("2026-08-31T23:59:59Z", "2025/2026")]
    [InlineData("2026-09-01T00:00:00Z", "2026/2027")]
    public async Task Suggested_year_changes_in_september_on_server_clock(string utc, string expected)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var original = factory.CreateClient(); await Login(original, "admin-a@example.test");
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<TimeProvider>(new FixedClock(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture)))));
        using var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = original.DefaultRequestHeaders.Authorization;
        Assert.Equal(expected, (await Get(client, Setup)).GetProperty("suggestedAcademicYear").GetString());
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 2)]
    public async Task Legacy_count_includes_inactive_but_excludes_deleted_accounts(bool deleted, int expected)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            var account = await db.Users.SingleAsync(x => x.Email == "student-a@example.test");
            if (deleted) account.DeletedAt = DateTime.UtcNow;
            else db.Entry(account).Property(x => x.IsActive).CurrentValue = false;
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(); await Login(client, "admin-a@example.test");
        Assert.Equal(expected, (await Get(client, Subscription)).GetProperty("usedAccounts").GetInt32());
    }

    [Theory]
    [InlineData("name", "")]
    [InlineData("institutionType", "Unknown")]
    [InlineData("academicYear", "２０２６/２０２７")]
    [InlineData("rowVersion", 0)]
    public async Task Invalid_setup_fields_preserve_pending_state(string field, object value)
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync();
        using var client = factory.CreateClient(); await Login(client, "admin-a@example.test");
        var request = new Dictionary<string, object?> { ["name"] = "Instituição", ["institutionType"] = "School", ["locality"] = null, ["academicYear"] = "2026/2027", ["rowVersion"] = 1 };
        request[field] = value;
        using var invalid = await client.PutAsJsonAsync(Setup, request); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var pending = await Get(client, Setup); Assert.False(pending.GetProperty("isComplete").GetBoolean()); Assert.Equal(1, pending.GetProperty("rowVersion").GetInt64());
    }

    private sealed class FixedClock(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
    private static async Task<JsonElement> Get(HttpClient client, string route)
    { using var response = await client.GetAsync(route); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private static async Task Login(HttpClient client, string email)
    {
        client.DefaultRequestHeaders.Authorization = null;
        using var response = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest(email, IdentityFactory.Password)); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
    }
    private static async Task<DateTime?> Completion(IdentityFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().InstitutionProfiles.IgnoreQueryFilters().Select(x => EF.Property<DateTime?>(x, "CompletedAt")).SingleAsync();
    }
    private sealed class FailingAudit : IAuditWriter
    {
        public void Append(AuditEvent auditEvent) => throw new InvalidOperationException("Injected audit failure");
        public Task AppendIndependentAsync(AuditEvent auditEvent, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }
}
