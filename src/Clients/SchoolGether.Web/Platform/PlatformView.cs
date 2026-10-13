using System.Net;
using Microsoft.AspNetCore.Components;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Platform;

namespace SchoolGether.Web.Platform;

// Every request belongs to this rendered view. Disposed views cannot apply late results.
public abstract class PlatformView : ComponentBase, IDisposable
{
    [Parameter] public PlatformContextResponse Context { get; set; } = default!;
    [Parameter] public EventCallback PermissionsChanged { get; set; }
    protected readonly CancellationTokenSource Lifetime = new();
    protected bool Busy { get; private set; }
    protected string? Error { get; private set; }
    protected string? Notice { get; set; }
    protected bool Disposed { get; private set; }
    private int request;
    protected static string T(string key) => UiMessages.Get(key);
    protected bool Can(string permission) => Context.IsMaster || Context.Permissions.Contains(permission);
    protected async Task RunAsync<TValue>(Func<CancellationToken, Task<TValue>> operation, Action<TValue> apply)
    {
        if (Busy || Disposed) return;
        var current = ++request;
        Busy = true;
        Error = null;
        Notice = null;
        try
        {
            var value = await operation(Lifetime.Token);
            if (!Disposed && current == request) apply(value);
        }
        catch (OperationCanceledException) when (Lifetime.IsCancellationRequested) { }
        catch (SessionExpiredException) { /* Home observes the shared session and returns to login. */ }
        catch (Exception exception)
        {
            if (Disposed || current != request) return;
            Error = T(exception switch
            {
                IdentityApiException { StatusCode: HttpStatusCode.Forbidden } => "PlatformForbidden",
                IdentityApiException { StatusCode: HttpStatusCode.Conflict } => "PlatformConflict",
                IdentityApiException { StatusCode: HttpStatusCode.BadRequest } => "PlatformInvalid",
                HttpRequestException or OperationCanceledException => "NetworkError",
                _ => "ServiceError"
            });
            if (exception is IdentityApiException { StatusCode: HttpStatusCode.Forbidden })
                await PermissionsChanged.InvokeAsync();
        }
        finally { if (!Disposed && current == request) Busy = false; }
    }
    public virtual void Dispose()
    {
        Disposed = true;
        request++;
        Lifetime.Cancel();
        Lifetime.Dispose();
    }
}
