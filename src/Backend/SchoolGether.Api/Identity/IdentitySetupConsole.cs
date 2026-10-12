using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace SchoolGether.Api.Identity;

internal static class IdentitySetupConsole
{
    internal static async Task RunAsync(IConfiguration configuration, string contentRoot, IStringLocalizer<ApiMessages> messages)
    {
        if (!string.IsNullOrWhiteSpace(configuration["Jwt:PrivateKeyPem"]))
        {
            Console.WriteLine(messages["IdentityKeyExists"]);
            return;
        }
        using var rsa = RSA.Create(3072);
        var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            CreateNoWindow = true };
        foreach (var argument in new[] { "user-secrets", "set", "--project", Path.Combine(contentRoot, "SchoolGether.Api.csproj") })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException(messages["IdentityKeyFailed"]);
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(
            new Dictionary<string, string> { ["Jwt:PrivateKeyPem"] = rsa.ExportRSAPrivateKeyPem() }));
        process.StandardInput.Close();
        await process.WaitForExitAsync();
        await Task.WhenAll(output, errors);
        if (process.ExitCode != 0) throw new InvalidOperationException(messages["IdentityKeyFailed"]);
        Console.WriteLine(messages["IdentityKeyCreated"]);
    }
}
