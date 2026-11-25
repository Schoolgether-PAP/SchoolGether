using Microsoft.AspNetCore.Components;
using SchoolGether.ApiClient.Identity;
using System.Net;

namespace SchoolGether.Web.Academic;

// A context owns all its reads and writes. Even a handler that ignores cancellation
// cannot publish a result into a different class or authenticated session.
public abstract class EnrolmentView : ComponentBase, IDisposable
{
    [Inject] protected IdentitySession Session { get; set; } = default!;
    [Parameter] public int CourseId { get; set; }
    [Parameter] public int ClassId { get; set; }
    private ViewContext? context;
    private bool disposed;
    protected bool Busy;
    protected string? Error;
    protected bool Active => context is not null && IsCurrent(context);
    protected ViewContext Context => context!;
    protected static string T(string key) => UiMessages.Get(key);

    protected override void OnInitialized() => Session.Changed += SessionChanged;

    protected override async Task OnParametersSetAsync()
    {
        if (context is not null && context.CourseId == CourseId && context.ClassId == ClassId && context.Generation == Session.Generation) return;
        context?.Dispose();
        context = new(CourseId, ClassId, Session.Generation);
        Busy = false;
        Error = null;
        await InitializeContextAsync();
    }

    protected abstract Task InitializeContextAsync();
    protected bool IsCurrent(ViewContext value) => !disposed && ReferenceEquals(context, value)
        && !value.Token.IsCancellationRequested && value.CourseId == CourseId && value.ClassId == ClassId
        && value.Generation == Session.Generation && Session.Account?.Role == "InstitutionAdmin";

    private void SessionChanged()
    {
        if (context?.Generation == Session.Generation) return;
        context?.Dispose();
        _ = InvokeAsync(StateHasChanged);
    }

    protected void RequestFailed(Exception error)
    {
        Error = T(error switch
        {
            IdentityApiException { StatusCode: HttpStatusCode.Forbidden } => "EnrolmentForbidden",
            IdentityApiException { StatusCode: HttpStatusCode.NotFound } => "EnrolmentNotFound",
            IdentityApiException { StatusCode: HttpStatusCode.BadRequest } => "EnrolmentInvalid",
            IdentityApiException { StatusCode: HttpStatusCode.Conflict } => "EnrolmentConflictHelp",
            IdentityApiException { StatusCode: HttpStatusCode.TooManyRequests } => "RateLimit",
            HttpRequestException or OperationCanceledException => "NetworkError",
            _ => "ServiceError"
        });
    }

    public virtual void Dispose()
    {
        disposed = true;
        Session.Changed -= SessionChanged;
        context?.Dispose();
    }

    protected sealed class ViewContext : IDisposable
    {
        private readonly CancellationTokenSource cancellation = new();
        private bool disposed;
        public ViewContext(int courseId, int classId, long generation)
        {
            CourseId = courseId; ClassId = classId; Generation = generation;
            Token = cancellation.Token;
        }
        public int CourseId { get; }
        public int ClassId { get; }
        public long Generation { get; }
        public CancellationToken Token { get; }
        public void Dispose() { if (disposed) return; disposed = true; cancellation.Cancel(); cancellation.Dispose(); }
    }
}
