using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Identity;
using SchoolGether.Contracts.Identity;
using SchoolGether.Contracts.Platform;
using SchoolGether.Domain.Audit;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class InstitutionCapacityTests
{
    private const string UsersRoute = "/api/v1/identity/users";

    [Fact]
    public async Task Last_seat_is_reserved_and_next_creation_is_rejected()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var institutionId = await Register(factory, client);
        await Authenticate(client, "capacity-owner@example.test");
        using var success = await client.PostAsJsonAsync(UsersRoute, User("last@example.test"));
        Assert.Equal(HttpStatusCode.Created, success.StatusCode);
        using var denied = await client.PostAsJsonAsync(UsersRoute, User("excess@example.test"));
        await AssertCapacityError(denied);
        await AssertCounts(factory, institutionId, 2, 2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Duplicate_or_invalid_user_does_not_consume_seat(bool duplicate)
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var institutionId = await Register(factory, client);
        await Authenticate(client, "capacity-owner@example.test");
        var request = duplicate ? User("capacity-owner@example.test") : User("invalid@example.test") with { Password = "abcdefghijklmnop" };
        using var failure = await client.PostAsJsonAsync(UsersRoute, request);
        Assert.Equal(duplicate ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, failure.StatusCode);
        await AssertCounts(factory, institutionId, 1, 1);
        using var success = await client.PostAsJsonAsync(UsersRoute, User("after-failure@example.test"));
        Assert.Equal(HttpStatusCode.Created, success.StatusCode);
        await AssertCounts(factory, institutionId, 2, 2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Global_admin_creation_obeys_same_capacity(bool dedicatedProvisioning)
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var institutionId = await Register(factory, client);
        await Authenticate(client, "platform@example.test");
        async Task<HttpResponseMessage> Create(string email) => dedicatedProvisioning
            ? await client.PostAsJsonAsync($"{PlatformInstitutionTests.Root}/{institutionId}/administrators",
                new CreateInstitutionAdminRequest { Email = email, Password = IdentityFactory.Password, Name = "Responsável", Reason = "Designação" })
            : await client.PostAsJsonAsync(UsersRoute, User(email) with { InstitutionId = institutionId });
        using var success = await Create("global-last@example.test");
        Assert.Equal(HttpStatusCode.Created, success.StatusCode);
        using var denied = await Create("global-excess@example.test");
        await AssertCapacityError(denied);
        await AssertCounts(factory, institutionId, 2, 2);
    }

    [Fact]
    public async Task Legacy_institution_without_subscription_still_accepts_account()
    {
        await using var factory = new IdentityFactory();
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        await Authenticate(client, "admin-a@example.test");
        using var success = await client.PostAsJsonAsync(UsersRoute, User("legacy-new@example.test"));
        Assert.Equal(HttpStatusCode.Created, success.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        Assert.Empty(await db.Subscriptions.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(4, await db.Members.IgnoreQueryFilters().CountAsync(x => x.InstitutionId == factory.InstitutionA));
    }

    [Fact]
    public async Task Audit_failure_rolls_back_reserved_seat_and_account()
    {
        var failure = new FailingAudit();
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var institutionId = await Register(factory, client);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<SchoolGetherDbContext>(options => options.AddInterceptors(failure))));
        using var global = host.CreateClient();
        await Authenticate(global, "platform@example.test");
        failure.Enabled = true;
        using var response = await global.PostAsJsonAsync(UsersRoute, User("audit-rollback@example.test") with { InstitutionId = institutionId });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertCounts(factory, institutionId, 1, 1);
        failure.Enabled = false;
        using var success = await global.PostAsJsonAsync(UsersRoute, User("audit-retry@example.test") with { InstitutionId = institutionId });
        Assert.Equal(HttpStatusCode.Created, success.StatusCode);
        await AssertCounts(factory, institutionId, 2, 2);
    }

    [Fact]
    public async Task Inactive_account_keeps_its_seat_and_other_tenant_cannot_consume_it()
    {
        await using var factory = new IdentityFactory(onboardingEnabled: true);
        await factory.SeedAsync();
        using var client = factory.CreateClient();
        var institutionId = await Register(factory, client);
        await Authenticate(client, "admin-a@example.test");
        using var crossTenant = await client.PostAsJsonAsync(UsersRoute, User("cross-tenant@example.test") with { InstitutionId = institutionId });
        Assert.Equal(HttpStatusCode.Forbidden, crossTenant.StatusCode);
        await AssertCounts(factory, institutionId, 1, 1);
        await Authenticate(client, "capacity-owner@example.test");
        using var created = await client.PostAsJsonAsync(UsersRoute, User("inactive@example.test"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            var account = await db.Users.SingleAsync(x => x.Email == "inactive@example.test");
            db.Entry(account).Property(x => x.IsActive).CurrentValue = false;
            await db.SaveChangesAsync();
        }
        using var denied = await client.PostAsJsonAsync(UsersRoute, User("after-inactive@example.test"));
        await AssertCapacityError(denied);
        await AssertCounts(factory, institutionId, 2, 2);
    }

    [MySqlFact]
    public async Task One_remaining_seat_allows_only_one_concurrent_creation()
    {
        await using var factory = new IdentityFactory(mySqlConnectionString: Environment.GetEnvironmentVariable("SCHOOLGETHER_TEST_MYSQL"), onboardingEnabled: true);
        await factory.SeedAsync();
        using var setup = factory.CreateClient();
        var institutionId = await Register(factory, setup);
        var race = new ReservationRace();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<SchoolGetherDbContext>(options => options.AddInterceptors(race))));
        using var first = host.CreateClient();
        using var second = host.CreateClient();
        await Authenticate(first, "capacity-owner@example.test");
        await Authenticate(second, "platform@example.test");
        // Distinct e-mails: a 409 must come from capacity, never duplicate identity.
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Create(HttpClient client, string email, int? target)
        {
            await start.Task;
            return await client.PostAsJsonAsync(UsersRoute, User(email) with { InstitutionId = target });
        }
        var a = Create(first, "race-school@example.test", null);
        var b = Create(second, "race-platform@example.test", institutionId);
        start.SetResult();
        var responses = await Task.WhenAll(a, b);
        try
        {
            Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, responses.Select(x => x.StatusCode).Order().ToArray());
            Assert.Equal(2, race.Connections.Count);
            Assert.NotSame(race.Connections.ElementAt(0), race.Connections.ElementAt(1));
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
            Assert.Equal("MySql.EntityFrameworkCore", db.Database.ProviderName);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            await AssertCapacityError(responses.Single(x => x.StatusCode == HttpStatusCode.Conflict));
            await AssertCounts(factory, institutionId, 2, 2);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    private static CreateUserRequest User(string email) => new(email, IdentityFactory.Password, "Aluno", "Student", null);

    private static async Task<int> Register(IdentityFactory factory, HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(OnboardingTests.Route, OnboardingTests.Request("capacity-owner@example.test"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var institutionId = result.GetProperty("institutionId").GetInt32();
        using var scope = factory.Services.CreateScope();
        await using var db = new SchoolGetherDbContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<SchoolGetherDbContext>>(),
            new Owner(result.GetProperty("ownerId").GetInt32(), institutionId));
        var subscription = await db.Subscriptions.SingleAsync();
        // Small deterministic quota; the public server catalogue remains unchanged.
        db.Entry(subscription).Property(x => x.AccountLimit).CurrentValue = 2;
        await db.SaveChangesAsync();
        await AssertCounts(factory, institutionId, 1, 1);
        return institutionId;
    }

    private static async Task Authenticate(HttpClient client, string email)
    {
        client.DefaultRequestHeaders.Authorization = null;
        using var response = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest(email, IdentityFactory.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    private static async Task AssertCounts(IdentityFactory factory, int institutionId, int expected, long version)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>();
        var subscription = await db.Subscriptions.IgnoreQueryFilters().SingleAsync(x => x.InstitutionId == institutionId);
        Assert.Equal(expected, subscription.UsedAccounts);
        Assert.Equal(version, subscription.RowVersion);
        Assert.Equal(expected, await db.Members.IgnoreQueryFilters().CountAsync(x => x.InstitutionId == institutionId));
        Assert.Equal(6 + expected, await db.Users.CountAsync());
        if (expected > 1) Assert.NotNull(subscription.UpdatedAt);
    }

    private static async Task AssertCapacityError(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("limite de contas", problem.GetProperty("title").GetString());
    }

    private sealed class Owner(int userId, int institutionId) : ICurrentAccount
    {
        public int? UserId => userId;
        public int? InstitutionId => institutionId;
        public string? Role => "InstitutionAdmin";
    }

    private sealed class ReservationRace : DbCommandInterceptor
    {
        private readonly TaskCompletionSource bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentBag<DbConnection> Connections { get; } = new();

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("demonstration_subscription", StringComparison.Ordinal))
            {
                Connections.Add(command.Connection!);
                if (Connections.Count == 2) bothArrived.TrySetResult();
                // Synchronize the actual quota commands before either reaches MySQL.
                await bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }
    private sealed class FailingAudit : SaveChangesInterceptor
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
