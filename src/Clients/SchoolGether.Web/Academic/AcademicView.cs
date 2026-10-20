using Microsoft.AspNetCore.Components;
using SchoolGether.ApiClient.Identity;
using System.Net;

namespace SchoolGether.Web.Academic;

public abstract class AcademicView : ComponentBase, IDisposable
{
    [Inject] protected IdentitySession Session { get; set; } = default!;
    protected readonly CancellationTokenSource Lifetime = new();
    private long generation;
    protected bool Active => !Lifetime.IsCancellationRequested && Session.Generation == generation && Session.Account?.Role == "InstitutionAdmin";
    protected bool Busy;
    protected string? Error;
    protected static string T(string key) => UiMessages.Get(key);
    protected override void OnInitialized() { generation = Session.Generation; Session.Changed += SessionChanged; }
    private void SessionChanged() { if (Session.Generation != generation) Lifetime.Cancel(); }
    protected void RequestFailed(Exception e) {
        if (!Active) return;
        Error = T(e switch {
            IdentityApiException { StatusCode: HttpStatusCode.Forbidden } => "CourseForbidden",
            IdentityApiException { StatusCode: HttpStatusCode.NotFound } => "CourseNotFound",
            IdentityApiException { StatusCode: HttpStatusCode.BadRequest } => "CourseInvalid",
            IdentityApiException { StatusCode: HttpStatusCode.Conflict } => "CourseDuplicate",
            IdentityApiException { StatusCode: HttpStatusCode.TooManyRequests } => "RateLimit",
            HttpRequestException or OperationCanceledException => "NetworkError", _ => "ServiceError" });
    }
    public virtual void Dispose() { Session.Changed -= SessionChanged; Lifetime.Cancel(); Lifetime.Dispose(); }
}
