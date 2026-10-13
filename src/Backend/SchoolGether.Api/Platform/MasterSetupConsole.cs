using Microsoft.Extensions.Localization;
using SchoolGether.Application.Platform;

namespace SchoolGether.Api.Platform;

public static class MasterSetupConsole
{
    public static async Task<bool> RunAsync(IServiceProvider services, bool isDevelopment, TextReader input, TextWriter output,
        CancellationToken cancellationToken)
    {
        var messages = services.GetRequiredService<IStringLocalizer<ApiMessages>>();
        if (!isDevelopment)
        {
            await output.WriteLineAsync(messages["MasterDevelopmentOnly"]);
            return false;
        }
        await output.WriteAsync(messages["MasterAccountId"]);
        var idText = await input.ReadLineAsync(cancellationToken);
        await output.WriteAsync(messages["MasterConfirmId"]);
        var confirmationText = await input.ReadLineAsync(cancellationToken);
        int.TryParse(idText, out var id);
        int.TryParse(confirmationText, out var confirmation);
        using var scope = services.CreateScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<MasterPromotionService>().PromoteAsync(id, confirmation, cancellationToken);
            await output.WriteLineAsync(messages["MasterPromoted"]);
            return true;
        }
        catch (PlatformException exception)
        {
            await output.WriteLineAsync(messages[exception.Code]);
            return false;
        }
    }
}
