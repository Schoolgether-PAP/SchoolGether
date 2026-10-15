using System.Net;
using Microsoft.AspNetCore.Components;
using SchoolGether.ApiClient.Identity;
namespace SchoolGether.Web.Institutions;
public abstract class InstitutionView : ComponentBase, IDisposable
{
    [Inject] protected IdentitySession Session { get; set; } = default!;
    protected readonly CancellationTokenSource Lifetime = new();
    private int? accountId;
    protected bool Active => !Lifetime.IsCancellationRequested && Session.Account?.Id == accountId;
    protected static string T(string key) => UiMessages.Get(key);
    protected string? Error;
    protected bool Busy;
    protected override void OnInitialized() { accountId = Session.Account?.Id; Session.Changed += SessionChanged; }
    private void SessionChanged() { if (Session.Account?.Id != accountId) Lifetime.Cancel(); }
    protected void RequestFailed(Exception exception, string conflictKey = "InstitutionConflict")
    {
        if (!Active) return;
        Error = T(exception switch {
            IdentityApiException { StatusCode: HttpStatusCode.Conflict } => conflictKey,
            IdentityApiException { StatusCode: HttpStatusCode.BadRequest } => "InstitutionInvalid",
            IdentityApiException { StatusCode: HttpStatusCode.TooManyRequests } => "RateLimit",
            HttpRequestException or OperationCanceledException => "NetworkError", _ => "ServiceError" });
    }
    public virtual void Dispose() { Session.Changed -= SessionChanged; Lifetime.Cancel(); Lifetime.Dispose(); }
}
