using System.Text;
using Microsoft.Extensions.Localization;
using SchoolGether.Application.Identity;

namespace SchoolGether.Api.Identity;

internal static class BootstrapConsole
{
    internal static async Task RunAsync(IServiceProvider services)
    {
        var messages = services.GetRequiredService<IStringLocalizer<ApiMessages>>();
        Console.Write(messages["BootstrapEmail"]);
        var email = Console.ReadLine() ?? string.Empty;
        Console.Write(messages["BootstrapPassword"]);
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (password.Length > 0) password.Length--; }
            else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar);
        }
        Console.WriteLine();
        using var scope = services.CreateScope();
        try
        {
            var created = await scope.ServiceProvider.GetRequiredService<IPlatformAdminBootstrap>()
                .CreateAsync(email, password.ToString(), CancellationToken.None);
            Console.WriteLine(messages[created ? "BootstrapCreated" : "BootstrapExists"]);
        }
        catch (IdentityException)
        {
            Console.WriteLine(messages["InvalidAccount"]);
            Environment.ExitCode = 1;
        }
        finally { password.Clear(); }
    }
}
