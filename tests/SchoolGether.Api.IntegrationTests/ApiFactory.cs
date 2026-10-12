using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolGether.Application.Diagnostics;

namespace SchoolGether.Api.IntegrationTests;

public sealed class ApiFactory(
    string environment = "Development", bool databaseReady = true, bool databaseThrows = false)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
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
