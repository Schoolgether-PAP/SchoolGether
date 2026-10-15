using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SchoolGether.Infrastructure.Persistence;
using SchoolGether.Application.Audit;
using SchoolGether.Contracts.Identity;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Onboarding;
using SchoolGether.Application.Institutions;
using SchoolGether.Domain.Billing;
using SchoolGether.Domain.Institutions;
using SchoolGether.Domain.Identity;

namespace SchoolGether.Api.IntegrationTests;

public sealed class OnboardingTests
{
    internal static Dictionary<string, object?> Request(string email = "new-owner@example.test") => new()
    {
        ["planId"] = "essential", ["catalogVersion"] = "2026-10-14-v1",
        ["ownerName"] = "Responsável", ["email"] = email, ["password"] = IdentityFactory.Password,
        ["institutionName"] = "Escola Nova", ["institutionType"] = "School", ["locality"] = "Lisboa",
        ["acceptDemonstration"] = true
    };
    internal const string Route = "/api/v1/onboarding/institutions";

    [Fact]
    public async Task Signup_creates_only_institution_owner_and_demonstration_subscription()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.EnumerateObject().Count());
        Assert.Equal("new-owner@example.test", body.GetProperty("email").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var owner = await db.Users.SingleAsync(x => x.Email == "new-owner@example.test");
        Assert.False(owner.IsPlatformAdmin);
        Assert.Equal(owner.Id, owner.CreatedBy);
        var institution = await db.Institutions.SingleAsync(x => x.Id == body.GetProperty("institutionId").GetInt32());
        Assert.Equal(owner.Id, institution.CreatedBy);
        var member = await db.Members.IgnoreQueryFilters().SingleAsync(x => x.UserId == owner.Id);
        Assert.Equal("InstitutionAdmin", member.Role.ToString());
        Assert.Equal(owner.Id, member.CreatedBy);
        Assert.Equal(institution.Id, member.InstitutionId);
        Assert.Empty(await db.Sessions.Where(x => x.UserId == owner.Id).ToListAsync());
        var audit = Assert.Single(await db.AuditEntries.Where(x => x.InstitutionId == institution.Id).ToListAsync());
        Assert.Equal(owner.Id, audit.ActorId);
        Assert.DoesNotContain(owner.Email!, audit.After!);
        Assert.DoesNotContain(IdentityFactory.Password, audit.After!);
        Assert.Contains("essential", audit.After!);
        var subscription = await db.Subscriptions.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(institution.Id, subscription.InstitutionId);
        Assert.Equal("Demonstration", subscription.Status);
        Assert.Equal("essential", subscription.PlanId);
        Assert.Equal("PlanEssential", subscription.PlanNameKey);
        Assert.Equal("2026-10-14-v1", subscription.CatalogVersion);
        Assert.Equal(59m, subscription.MonthlyPrice);
        Assert.Equal("EUR", subscription.Currency);
        Assert.Equal(100, subscription.AccountLimit);
        Assert.Equal(1, subscription.UsedAccounts);
        Assert.Equal(owner.Id, subscription.CreatedBy);
        var profile = await db.InstitutionProfiles.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(institution.Id, profile.InstitutionId);
        Assert.Equal("School", profile.InstitutionType);
        Assert.Equal("Lisboa", profile.Locality);
        Assert.Equal(owner.Id, profile.CreatedBy);
        Assert.False(profile.IsComplete);
        Assert.Null(profile.AcademicYear);
        Assert.Empty(await db.Subscriptions.ToListAsync());
        Assert.Empty(await db.InstitutionProfiles.ToListAsync());
    }

    [Theory]
    [InlineData("ownerName", "")]
    [InlineData("email", "invalid")]
    [InlineData("password", "short")]
    [InlineData("password", "aaaaaaaaaaaa1!")]
    [InlineData("password", "AAAAAAAAAAAA1!")]
    [InlineData("password", "NoNumberHere!!")]
    [InlineData("password", "NoSymbolsHere1")]
    [InlineData("institutionName", "")]
    [InlineData("institutionType", "Unknown")]
    [InlineData("acceptDemonstration", false)]
    public async Task Invalid_signup_leaves_no_rows(string field, object value)
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var request = Request(); request[field] = value;
        using var response = await client.PostAsJsonAsync(Route, request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUnchanged(factory);
    }

    [Theory]
    [InlineData("student-a@example.test")]
    [InlineData("STUDENT-A@EXAMPLE.TEST")]
    public async Task Duplicate_email_does_not_convert_account_or_create_institution(string email)
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, Request(email));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await AssertUnchanged(factory);
    }

    [Fact]
    public async Task Failure_writing_audit_rolls_back_user_and_institution()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true, auditWriter: new FailingAudit());
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, Request());
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        await AssertUnchanged(factory);
    }

    [Fact]
    public async Task Authenticated_signup_is_refused()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest("student-a@example.test", IdentityFactory.Password));
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        using var response = await client.PostAsJsonAsync(Route, Request());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertUnchanged(factory);
    }

    [Fact]
    public async Task Disabled_signup_returns_503_without_writes()
    {
        await using var factory = new IdentityFactory();
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, Request());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertUnchanged(factory);
    }

    [Theory]
    [InlineData("catalogVersion", "old", HttpStatusCode.Conflict)]
    [InlineData("planId", "unknown", HttpStatusCode.BadRequest)]
    public async Task Catalog_conflicts_do_not_create_rows(string field, string value, HttpStatusCode status)
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var request = Request(); request[field] = value;
        using var response = await client.PostAsJsonAsync(Route, request);
        Assert.Equal(status, response.StatusCode);
        await AssertUnchanged(factory);
    }

    [Fact]
    public async Task Anonymous_write_scope_cannot_target_existing_institution()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IOnboardingUnitOfWork>();
        var accounts = scope.ServiceProvider.GetRequiredService<IOnboardingAccountProvisioner>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteAsync(async token =>
        {
            var owner = await accounts.CreateOwnerAsync("Novo", "scope@example.test", IdentityFactory.Password, token);
            await accounts.LinkInstitutionAsync(owner.Id, factory.InstitutionA, token);
            return owner.Id;
        }, default));
        await AssertUnchanged(factory);
        // A capacidade termina mesmo após falha: o provisionador não pode ser usado fora da transação.
        await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.CreateOwnerAsync("Novo", "after@example.test", IdentityFactory.Password, default));
    }

    [Fact]
    public async Task Anonymous_scope_cannot_use_existing_owner_or_modify_existing_institution()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IOnboardingUnitOfWork>();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var oldOwner = await db.Users.FirstAsync();
        var institutions = scope.ServiceProvider.GetRequiredService<IInstitutionRegistrationStore>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteAsync(
            token => institutions.CreateAsync("Inválida", "School", null, oldOwner.Id, token), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteAsync(async token =>
        {
            var old = await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionA, token);
            old.Rename("Alteração proibida");
            await db.SaveChangesAsync(token);
            return old.Id;
        }, default));
        Assert.Equal("Instituição A", (await db.Institutions.SingleAsync(x => x.Id == factory.InstitutionA)).Name);
        await AssertUnchanged(factory);
    }

    [Fact]
    public async Task Anonymous_scope_cannot_reinsert_bound_membership_into_existing_institution()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var platformId = await db.Users.Where(x => x.IsPlatformAdmin).Select(x => x.Id).SingleAsync();
        var accounts = scope.ServiceProvider.GetRequiredService<IOnboardingAccountProvisioner>();
        var institutions = scope.ServiceProvider.GetRequiredService<IInstitutionRegistrationStore>();
        var uow = scope.ServiceProvider.GetRequiredService<IOnboardingUnitOfWork>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteAsync(async token =>
        {
            var owner = await accounts.CreateOwnerAsync("Novo", "scope@example.test", IdentityFactory.Password, token);
            var id = await institutions.CreateAsync("Nova", "School", null, owner.Id, token);
            await accounts.LinkInstitutionAsync(owner.Id, id, token);
            var member = db.Members.Local.Single();
            db.Entry(member).State = EntityState.Detached;
            db.Entry(member).Property(x => x.Id).CurrentValue = 0;
            db.Entry(member).Property(x => x.UserId).CurrentValue = platformId;
            db.Entry(member).Property(x => x.InstitutionId).CurrentValue = factory.InstitutionA;
            db.Members.Add(member);
            return id;
        }, default));
        await AssertUnchanged(factory);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("subscription")]
    [InlineData("member")]
    public async Task Anonymous_writes_without_scope_remain_denied(string entity)
    {
        await using var factory = new IdentityFactory();
        await factory.SeedAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        if (entity == "profile") db.InstitutionProfiles.Add(new InstitutionProfile(factory.InstitutionA, "School", null, 1));
        if (entity == "subscription") db.Subscriptions.Add(new DemonstrationSubscription(factory.InstitutionA,
            new SubscriptionPlan("essential", "PlanEssential", 59m, "EUR", 100), "2026-10-14-v1", 1));
        if (entity == "member") db.Members.Add(new InstitutionMember(1, factory.InstitutionA, "Proibido", AccountRole.InstitutionAdmin, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Oversized_body_is_rejected_without_writes(bool knownLength)
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        using var body = new StringContent(JsonSerializer.Serialize(Request()) + new string(' ', 17000), System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        if (knownLength) response = await client.PostAsync(Route, body);
        else
        {
            var data = await body.ReadAsByteArrayAsync();
            using var streamed = new UnknownLengthContent(data);
            response = await client.PostAsync(Route, streamed);
        }
        using var ownedResponse = response;
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        await AssertUnchanged(factory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public async Task Fourth_request_is_limited_even_with_spoofed_forwarded_addresses(string suffix)
    {
        await using var factory = new IdentityFactory();
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        for (var i = 0; i < 4; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Route + suffix) { Content = JsonContent.Create(Request()) };
            request.Headers.Add("X-Forwarded-For", "192.0.2." + (i + 1));
            using var response = await client.SendAsync(request);
            Assert.Equal(i < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
        await AssertUnchanged(factory);
    }

    [Fact]
    public async Task Invalid_bearer_is_not_treated_as_anonymous_signup()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        using var response = await client.PostAsJsonAsync(Route, Request());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertUnchanged(factory);
    }

    [Fact]
    public async Task Global_limit_applies_across_distinct_remote_addresses()
    {
        await using var factory = new IdentityFactory();
        for (var i = 0; i < 21; i++)
        {
            var response = await factory.Server.SendAsync(context =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2." + (i + 1));
                context.Request.Method = "POST";
                context.Request.Path = Route;
                context.Request.ContentType = "application/json";
                context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Request())));
            });
            Assert.Equal(i < 20 ? 503 : 429, response.Response.StatusCode);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Deleted_or_inactive_email_is_not_reused(bool deleted)
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            var old = await db.Users.SingleAsync(x => x.Email == "student-a@example.test");
            if (deleted) old.DeletedAt = DateTime.UtcNow;
            else db.Entry(old).Property(x => x.IsActive).CurrentValue = false;
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, Request("student-a@example.test"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("student-a", error);
        Assert.DoesNotContain("Student", error);
        await AssertUnchanged(factory);
    }

    [Fact]
    public async Task Subscription_and_profile_remain_isolated_between_institutions()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        using var first = await client.PostAsJsonAsync(Route, Request());
        using var second = await client.PostAsJsonAsync(Route, Request("other-owner@example.test"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var result = await first.Content.ReadFromJsonAsync<JsonElement>();
        var institutionId = result.GetProperty("institutionId").GetInt32();
        using var scope = factory.Services.CreateScope();
        await using var db = new SchoolGetherDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<SchoolGetherDbContext>>(),
            new InstitutionAccount(result.GetProperty("ownerId").GetInt32(), institutionId));
        Assert.Equal(institutionId, (await db.Subscriptions.SingleAsync()).InstitutionId);
        Assert.Equal(institutionId, (await db.InstitutionProfiles.SingleAsync()).InstitutionId);
        var otherProfile = await db.InstitutionProfiles.IgnoreQueryFilters().SingleAsync(x => x.InstitutionId != institutionId);
        db.InstitutionProfiles.Remove(otherProfile);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var otherSubscription = await db.Subscriptions.IgnoreQueryFilters().SingleAsync(x => x.InstitutionId != institutionId);
        db.Subscriptions.Remove(otherSubscription);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var own = await db.InstitutionProfiles.SingleAsync();
        db.Entry(own).Property(x => x.InstitutionId).CurrentValue = otherProfile.InstitutionId;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] data;
        public UnknownLengthContent(byte[] data)
        {
            this.data = data;
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(data).AsTask();
    }

    private sealed class InstitutionAccount(int userId, int institutionId) : ICurrentAccount
    {
        public int? UserId => userId;
        public int? InstitutionId => institutionId;
        public string? Role => "InstitutionAdmin";
    }

    [Theory]
    [InlineData("ownerName", 151)]
    [InlineData("institutionName", 201)]
    [InlineData("locality", 101)]
    [InlineData("password", 129)]
    [InlineData("email", 257)]
    public async Task Oversized_fields_are_rejected_without_writes(string field, int length)
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var request = Request(); request[field] = new string('a', length);
        using var response = await client.PostAsJsonAsync(Route, request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUnchanged(factory);
    }

    private static async Task AssertUnchanged(IdentityFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Equal(6, await db.Users.CountAsync());
        Assert.Equal(2, await db.Institutions.CountAsync());
        Assert.Equal(5, await db.Members.IgnoreQueryFilters().CountAsync());
        Assert.Empty(await db.AuditEntries.ToListAsync());
        Assert.Empty(await db.Subscriptions.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.InstitutionProfiles.IgnoreQueryFilters().ToListAsync());
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Append(AuditEvent auditEvent) => throw new InvalidOperationException("Test audit failure");
        public Task AppendIndependentAsync(AuditEvent auditEvent, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }
}
