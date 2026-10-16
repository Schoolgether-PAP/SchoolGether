using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Api.IntegrationTests;

public sealed class SupportHubTests
{
    [Theory]
    [InlineData(HttpTransportType.LongPolling)]
    [InlineData(HttpTransportType.WebSockets)]
    public async Task HeaderAuthenticatedTransports_JoinOnlyVisibleTickets(HttpTransportType transport)
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true); await factory.SeedAsync();
        using var teacher = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(teacher, "teacher-a@example.test");
        var ticket = await SupportAuthorizationTests.Ticket(teacher); var id = ticket.GetProperty("id").GetInt32();
        using var other = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(other, "admin-b@example.test");
        var hidden = (await SupportAuthorizationTests.Ticket(other)).GetProperty("id").GetInt32();
        using var schoolAdmin = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(schoolAdmin, "admin-a@example.test");
        var colleague = (await SupportAuthorizationTests.Ticket(schoolAdmin)).GetProperty("id").GetInt32();
        await using var live = new SupportLiveTestClient(factory, teacher, transport);
        await live.StartAsync(); await live.JoinAsync(id);
        await Assert.ThrowsAsync<HubException>(() => live.JoinAsync(hidden));
        await Assert.ThrowsAsync<HubException>(() => live.JoinAsync(colleague));
        await live.Connection.InvokeAsync("LeaveTicket", id);
    }

    [Fact]
    public async Task StudentAndGlobalWithoutRead_CannotSubscribe()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true); await factory.SeedAsync();
        using var master = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(master);
        await PlatformAdminTests.Created(master, "none@example.test");
        foreach (var email in new[] { "student-a@example.test", "none@example.test" })
        {
            using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, email);
            await using var live = new SupportLiveTestClient(factory, client);
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            live.Connection.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
            try { await live.StartAsync(); }
            catch (HubException) { /* A recusa pode chegar antes de terminar o handshake. */ }
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HubConnectionState.Disconnected, live.Connection.State);
        }
    }

    [Fact]
    public async Task OriginGuard_RejectsUnapprovedOrigins_AndAllowsAuthorizedDeletePreflight()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "https://school.example.test" })));
        await factory.SeedAsync();
        using var client = configured.CreateClient();
        await PlatformAdminTests.Authenticate(client);
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Get, HttpMethod.Delete })
        {
            using var denied = new HttpRequestMessage(method, SupportLiveTestClient.Path + "/negotiate?negotiateVersion=1");
            denied.Headers.Add("Origin", "https://untrusted.example.test");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(denied)).StatusCode);
        }
        using var preflight = new HttpRequestMessage(HttpMethod.Options, SupportLiveTestClient.Path);
        preflight.Headers.Add("Origin", "https://school.example.test");
        preflight.Headers.Add("Access-Control-Request-Method", "DELETE");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization");
        using var response = await client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("DELETE", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Methods")));
        Assert.Equal("https://school.example.test", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task QueryBearerAlone_IsRejectedOnHubAndOtherRoutes()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client);
        var token = client.DefaultRequestHeaders.Authorization!.Parameter;
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(SupportLiveTestClient.Path + "/negotiate?negotiateVersion=1&access_token=" + token, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/identity/me?access_token=" + token)).StatusCode);
    }

    [Fact]
    public async Task AllowedOrigin_LongPollingUsesHeaders_AndAuthenticatedDeleteCleansUp()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "https://school.example.test" })));
        await factory.SeedAsync();
        using var client = configured.CreateClient(); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var id = (await SupportAuthorizationTests.Ticket(client)).GetProperty("id").GetInt32();
        await using var live = new SupportLiveTestClient(configured, client, origin: "https://school.example.test");
        await live.StartAsync(); await live.JoinAsync(id); await live.Connection.StopAsync();
        Assert.Contains(live.Requests, request => request.Method == HttpMethod.Delete && request.Status == 202 && request.HasAuthorization);
        Assert.All(live.Requests, request => { Assert.True(request.HasAuthorization); Assert.False(request.HasQueryToken); });
    }

    [Fact]
    public async Task ExpiredBearer_IsRejected_AndExistingWebSocketClosesAtExpiry()
    {
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true); await factory.SeedAsync();
        using var client = PlatformAdminTests.Client(factory); await PlatformAdminTests.Authenticate(client, "teacher-a@example.test");
        var id = (await SupportAuthorizationTests.Ticket(client)).GetProperty("id").GetInt32();
        var original = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WithExpiration(factory, original, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(SupportLiveTestClient.Path + "/negotiate?negotiateVersion=1", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WithExpiration(factory, original, DateTime.UtcNow.AddSeconds(3)));
        await using var live = new SupportLiveTestClient(factory, client, HttpTransportType.WebSockets);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        live.Connection.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
        await live.StartAsync(); await live.JoinAsync(id);
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(8));
        Assert.Equal(HubConnectionState.Disconnected, live.Connection.State);
    }

    private static string WithExpiration(IdentityFactory factory, string original, DateTime expires)
    {
        var old = new JwtSecurityTokenHandler().ReadJwtToken(original);
        using var rsa = RSA.Create(); rsa.ImportFromPem(factory.PrivateKey);
        var token = new JwtSecurityToken(old.Issuer, old.Audiences.Single(),
            old.Claims.Where(x => x.Type is not ("exp" or "nbf" or "iat" or "iss" or "aud")),
            DateTime.UtcNow.AddMinutes(-5), expires, new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task HubQueryAndBearer_AreAbsentFromErrorsApplicationLogsAndAudit()
    {
        const string sentinel = "PRIVATE-QUERY-SENTINEL";
        var logs = new CapturedLogs();
        await using var factory = new IdentityFactory(supportEnabled: true, concurrentSqlite: true);
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        await factory.SeedAsync();
        using var client = configured.CreateClient(); await PlatformAdminTests.Authenticate(client);
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        using var rejected = await client.PostAsync(SupportLiveTestClient.Path + "/negotiate?negotiateVersion=1&access_token=" + sentinel, null);
        // Um cabeçalho válido continua a autenticar; a query nunca é necessária nem reproduzida.
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.False((await rejected.Content.ReadAsStringAsync()).Contains(sentinel, StringComparison.Ordinal));
        client.DefaultRequestHeaders.Authorization = null;
        using var unauthorized = await client.PostAsync(SupportLiveTestClient.Path + "/negotiate?negotiateVersion=1&access_token=" + token, null);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.False((await unauthorized.Content.ReadAsStringAsync()).Contains(token, StringComparison.Ordinal));
        Assert.NotEmpty(logs.Messages);
        var logContainsSecret = logs.Messages.Any(value => value.Contains(token, StringComparison.Ordinal) || value.Contains(sentinel, StringComparison.Ordinal));
        Assert.False(logContainsSecret);
        using var scope = factory.Services.CreateScope();
        var audit = System.Text.Json.JsonSerializer.Serialize(await scope.ServiceProvider.GetRequiredService<SchoolGetherDbContext>().AuditEntries.ToListAsync());
        Assert.False(audit.Contains(token, StringComparison.Ordinal) || audit.Contains(sentinel, StringComparison.Ordinal));
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        internal ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(Messages);
        public void Dispose() { }
        private sealed class Capture(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + exception?.ToString());
        }
    }
}
