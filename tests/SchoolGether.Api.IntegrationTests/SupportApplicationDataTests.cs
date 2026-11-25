using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolGether.Application.Support;

namespace SchoolGether.Api.IntegrationTests;

public sealed class SupportApplicationDataTests
{
    [Fact]
    public async Task Application_data_storage_requires_explicit_opt_in_and_resolves_outside_webroot()
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        await using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Support:UseApplicationDataStorage"] = "true", ["Support:FilesRoot"] = ""
            })));
        var options = configured.Services.GetRequiredService<SupportOptions>();
        var environment = configured.Services.GetRequiredService<IWebHostEnvironment>();
        Assert.True(options.Enabled);
        Assert.Equal(Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "support")), options.FilesRoot);
        Assert.False(options.FilesRoot.StartsWith(Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Two_storage_locations_are_rejected_instead_of_silently_changing_existing_storage()
    {
        await using var factory = new IdentityFactory(supportEnabled: true);
        await using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Support:UseApplicationDataStorage"] = "true" })));
        Assert.Throws<InvalidOperationException>(() => configured.Services.GetRequiredService<SupportOptions>());
    }
}
