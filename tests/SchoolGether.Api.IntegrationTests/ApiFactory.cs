using Microsoft.AspNetCore.Hosting;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolGether.Application.Diagnostics;

namespace SchoolGether.Api.IntegrationTests;

public sealed class ApiFactory(
    string environment = "Development", bool databaseReady = true, bool databaseThrows = false)
    : WebApplicationFactory<Program>
{
    private static readonly string TestKey = CreateTestKey();
    private static string CreateTestKey()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> {
                ["Jwt:PrivateKeyPem"] = TestKey,
                // Diagnósticos usam o stub, nunca a ligação dos user-secrets locais.
                ["ConnectionStrings:SchoolGether"] = null
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDatabaseReadiness>();
            services.AddScoped<IDatabaseReadiness>(_ => new StubDatabaseReadiness(databaseReady, databaseThrows));
            // Estes endpoints de teste só existem no servidor em memória.
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
        });
    }

    private sealed class StubDatabaseReadiness(bool ready, bool throws) : IDatabaseReadiness
    {
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (throws)
            {
                throw new InvalidOperationException("private-test-marker");
            }

            return Task.FromResult(ready);
        }
    }
}
