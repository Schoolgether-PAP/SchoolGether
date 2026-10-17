using System.Net;
using Microsoft.AspNetCore.Components;
using SchoolGether.ApiClient.Identity;
namespace SchoolGether.Web.Support;
public abstract class SupportView : ComponentBase, IDisposable
{
    [Inject] protected IdentitySession Session { get; set; } = default!;
    protected CancellationTokenSource Lifetime { get; } = new();
    [Parameter] public EventCallback<bool> BusyChanged { get; set; }
    protected bool Disposed { get; private set; }
    protected bool Busy { get; private set; }
    protected string? Error { get; set; }
    protected static string T(string key) => UiMessages.Get(key);
    protected static string Label(string value) => T("Support" + value);
    protected static string Date(DateTime value) => value.ToLocalTime().ToString("g", System.Globalization.CultureInfo.GetCultureInfo("pt-PT"));
    protected async Task<bool> RunAsync(Func<Task> action)
    {
        if (Busy || Disposed) return false;
        Busy = true; Error = null;
        try { await BusyChanged.InvokeAsync(true); await action(); return !Disposed; }
        catch (OperationCanceledException) when (Lifetime.IsCancellationRequested) { }
        catch (SessionExpiredException) { }
        catch (Exception ex) { if (!Disposed) Error = Failure(ex); }
        finally { if (!Disposed) { Busy = false; await BusyChanged.InvokeAsync(false); } }
        return false;
    }
    protected static string Failure(Exception ex) => T(ex switch
    {
        IdentityApiException { StatusCode: HttpStatusCode.Conflict } => "SupportConflict",
        IdentityApiException { StatusCode: HttpStatusCode.Forbidden } => "SupportForbidden",
        IdentityApiException { StatusCode: HttpStatusCode.NotFound } => "SupportNotFound",
        IdentityApiException { StatusCode: HttpStatusCode.RequestEntityTooLarge } => "SupportTooLarge",
        IdentityApiException { StatusCode: HttpStatusCode.TooManyRequests } => "SupportRateLimit",
        IdentityApiException { StatusCode: HttpStatusCode.BadRequest } or ArgumentException => "SupportInvalid",
        HttpRequestException or OperationCanceledException => "NetworkError",
        _ => "ServiceError"
    });
    public virtual void Dispose() { Disposed = true; Lifetime.Cancel(); Lifetime.Dispose(); }
}
