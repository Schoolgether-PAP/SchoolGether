using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SchoolGether.Contracts.Identity;

namespace SchoolGether.Api.IntegrationTests;

public sealed class PlatformAdminTests
{
    internal static HttpClient Client(IdentityFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    internal static async Task Authenticate(HttpClient client, string email = "platform@example.test")
    {
        using var login = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest(email, IdentityFactory.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    internal static object Create(string email, params string[] permissions) => new {
        Email = email, Password = IdentityFactory.Password, Name = "Administrador", Permissions = permissions };

    internal static async Task<JsonElement> Created(HttpClient client, string email, params string[] permissions)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/platform/admins", Create(email, permissions));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    [Fact]
    public async Task SchoolAccount_IsDeniedEveryPlatformRoute()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        await Authenticate(client, "admin-a@example.test");
        foreach (var route in new[] { "context", "admins", "audit" })
        {
            using var response = await client.GetAsync("/api/v1/platform/" + route);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Master_CreatesSelectedCapabilitiesAndProtectsOwnAccount()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        await Authenticate(client);
        var context = await client.GetFromJsonAsync<JsonElement>("/api/v1/platform/context");
        Assert.True(context.GetProperty("isMaster").GetBoolean());
        var created = await Created(client, "colleague@example.test", "institutions.read", "audit.read");
        Assert.False(created.GetProperty("isMaster").GetBoolean());
        Assert.True(created.GetProperty("isActive").GetBoolean());
        Assert.Equal(new[] { "audit.read", "institutions.read" }, created.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()));
        using var own = await client.PutAsJsonAsync("/api/v1/platform/admins/" + context.GetProperty("accountId").GetInt32(),
            new { Name = "Alterado", IsActive = false, Permissions = Array.Empty<string>(), RowVersion = 1, Reason = "Tentativa" });
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
    }

    [Fact]
    public async Task AdminChanges_RejectUnknownDuplicateAndStaleVersion()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        await Authenticate(client);
        using var unknown = await client.PostAsJsonAsync("/api/v1/platform/admins", Create("unknown@example.test", "root"));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        var created = await Created(client, "colleague@example.test");
        using var duplicate = await client.PostAsJsonAsync("/api/v1/platform/admins", Create("COLLEAGUE@example.test"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var id = created.GetProperty("id").GetInt32();
        var update = new { Name = "Novo nome", IsActive = true, Permissions = new[] { "institutions.write" }, RowVersion = 1, Reason = "Gestão de clientes" };
        using var changed = await client.PutAsJsonAsync($"/api/v1/platform/admins/{id}", update);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var stale = await client.PutAsJsonAsync($"/api/v1/platform/admins/{id}", update);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var missing = await client.PutAsJsonAsync("/api/v1/platform/admins/999999", update);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task EmptyCapabilities_CannotBypassThroughLegacyRoutesOrManageColleagues()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var master = Client(factory);
        await Authenticate(master); var created = await Created(master, "empty@example.test");
        using var client = Client(factory); await Authenticate(client, "empty@example.test");
        using var context = await client.GetAsync("/api/v1/platform/context");
        Assert.Equal(HttpStatusCode.OK, context.StatusCode);
        using var admins = await client.GetAsync("/api/v1/platform/admins");
        Assert.Equal(HttpStatusCode.Forbidden, admins.StatusCode);
        using var audit = await client.GetAsync("/api/v1/platform/audit");
        Assert.Equal(HttpStatusCode.Forbidden, audit.StatusCode);
        using var institution = await client.PostAsJsonAsync("/api/v1/institutions", new { Name = "Contorno" });
        Assert.Equal(HttpStatusCode.Forbidden, institution.StatusCode);
        using var account = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "bypass@example.test", IdentityFactory.Password, "Contorno", "InstitutionAdmin", factory.InstitutionA));
        Assert.Equal(HttpStatusCode.Forbidden, account.StatusCode);
        using var create = await client.PostAsJsonAsync("/api/v1/platform/admins", Create("escalation@example.test"));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        using var update = await client.PutAsJsonAsync("/api/v1/platform/admins/" + created.GetProperty("id").GetInt32(),
            new { Name = "Elevado", IsActive = true, Permissions = new[] { "institutions.write" }, RowVersion = 1, Reason = "Contorno" });
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
    }

    [Fact]
    public async Task RevocationAndDisable_AffectSameJwtOnNextRequest()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var master = Client(factory);
        await Authenticate(master);
        var created = await Created(master, "writer@example.test", "institutions.write", "audit.read");
        var id = created.GetProperty("id").GetInt32();
        using var client = Client(factory); await Authenticate(client, "writer@example.test");
        using var allowed = await client.PostAsJsonAsync("/api/v1/institutions", new { Name = "Permitida" });
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        using var changed = await master.PutAsJsonAsync($"/api/v1/platform/admins/{id}",
            new { Name = "Revogado", IsActive = true, Permissions = new[] { "audit.read" }, RowVersion = 1, Reason = "Revogar escrita" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var denied = await client.PostAsJsonAsync("/api/v1/institutions", new { Name = "Recusada" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var deniedUser = await client.PostAsJsonAsync("/api/v1/identity/users", new CreateUserRequest(
            "revoked@example.test", IdentityFactory.Password, "Recusado", "Teacher", factory.InstitutionA));
        Assert.Equal(HttpStatusCode.Forbidden, deniedUser.StatusCode);
        using var disabled = await master.PutAsJsonAsync($"/api/v1/platform/admins/{id}",
            new { Name = "Inativo", IsActive = false, Permissions = new[] { "audit.read" }, RowVersion = 2, Reason = "Desativar acesso" });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        using var audit = await client.GetAsync("/api/v1/platform/audit");
        Assert.Equal(HttpStatusCode.Unauthorized, audit.StatusCode);
        using var profile = await client.GetAsync("/api/v1/identity/me");
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
    }

    [Fact]
    public async Task Audit_ContainsSanitizedMutationAndSensitiveReadsWithoutRecursion()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        await Authenticate(client); var created = await Created(client, "audited@example.test", "audit.read");
        using var changed = await client.PutAsJsonAsync("/api/v1/platform/admins/" + created.GetProperty("id").GetInt32(),
            new { Name = "Novo", IsActive = true, Permissions = Array.Empty<string>(), RowVersion = 1, Reason = "Retirar histórico" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var readAdmins = await client.GetAsync("/api/v1/platform/admins");
        Assert.Equal(HttpStatusCode.OK, readAdmins.StatusCode);
        var first = await client.GetFromJsonAsync<JsonElement>("/api/v1/platform/audit");
        var second = await client.GetFromJsonAsync<JsonElement>("/api/v1/platform/audit");
        Assert.Equal(first.GetProperty("total").GetInt32() + 1, second.GetProperty("total").GetInt32());
        var entries = second.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(entries, x => x.GetProperty("action").GetString() == "platform.admin.update" &&
            x.GetProperty("before").ValueKind != JsonValueKind.Null && x.GetProperty("after").ValueKind != JsonValueKind.Null);
        Assert.Contains(entries, x => x.GetProperty("action").GetString() == "platform.admin.list");
        var serialized = second.GetRawText();
        foreach (var secret in new[] { IdentityFactory.Password, "password", "securityStamp", "token", "connectionString" })
            Assert.DoesNotContain(secret, serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Audit_RecordsCapabilityValidationAndLegacyRoleRefusals()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var master = Client(factory);
        await Authenticate(master); await Created(master, "empty@example.test");
        using var empty = Client(factory); await Authenticate(empty, "empty@example.test");
        using var denied = await empty.PostAsJsonAsync("/api/v1/institutions", new { Name = "Recusada" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var unknown = await master.PostAsJsonAsync("/api/v1/platform/admins", Create("unknown@example.test", "root"));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        using var invalid = await master.PostAsJsonAsync("/api/v1/platform/admins", new { Password = IdentityFactory.Password });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var school = Client(factory); await Authenticate(school, "admin-a@example.test");
        using var legacy = await school.PostAsJsonAsync("/api/v1/institutions", new { Name = "Recusada" });
        Assert.Equal(HttpStatusCode.Forbidden, legacy.StatusCode);
        var history = await master.GetFromJsonAsync<JsonElement>("/api/v1/platform/audit?pageSize=100");
        var refusals = history.GetProperty("items").EnumerateArray().Where(x => x.GetProperty("result").GetString() == "refused").ToArray();
        Assert.Contains(refusals, x => x.GetProperty("reason").GetString() == "InvalidPermissions");
        Assert.Contains(refusals, x => x.GetProperty("reason").GetString() == "ValidationFailed");
        Assert.Contains(refusals, x => x.GetProperty("actorName").GetString() == "admin-a@example.test");
        Assert.Contains(refusals, x => x.GetProperty("actorName").GetString() == "Administrador");
        Assert.DoesNotContain(IdentityFactory.Password, history.GetRawText());
    }

    [Fact]
    public async Task Audit_SearchFiltersOnServerBeforePagination()
    {
        await using var factory = new IdentityFactory(); await factory.SeedAsync(); using var client = Client(factory);
        await Authenticate(client); var created = await Created(client, "search@example.test");
        using var update = await client.PutAsJsonAsync("/api/v1/platform/admins/" + created.GetProperty("id").GetInt32(),
            new { Name = "Pesquisa", IsActive = true, Permissions = Array.Empty<string>(), RowVersion = 1, Reason = "Revisão específica" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var results = await client.GetFromJsonAsync<JsonElement>("/api/v1/platform/audit?search=" + Uri.EscapeDataString("ESPECÍFICA") + "&pageSize=1");
        Assert.Equal(1, results.GetProperty("total").GetInt32());
        Assert.Equal("platform.admin.update", results.GetProperty("items")[0].GetProperty("action").GetString());
        var missing = await client.GetFromJsonAsync<JsonElement>("/api/v1/platform/audit?search=nonexistent-marker");
        Assert.Equal(0, missing.GetProperty("total").GetInt32());
        using var invalid = await client.GetAsync("/api/v1/platform/audit?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var refused = await client.GetFromJsonAsync<JsonElement>("/api/v1/platform/audit?search=InvalidPagination");
        Assert.Equal(1, refused.GetProperty("total").GetInt32());
        Assert.Equal("refused", refused.GetProperty("items")[0].GetProperty("result").GetString());
    }
}
