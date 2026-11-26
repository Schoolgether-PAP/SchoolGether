using SchoolGether.ApiClient.Identity;

namespace SchoolGether.Web.Learning;

/// <summary>Retains one immutable creation across area changes, only in the current authenticated session.</summary>
public sealed class PendingLearningCreation<T> : IDisposable where T : class
{
    private readonly IdentitySession session;
    private long generation;
    private (int? Id, int? Institution, string? Role) identity;
    public T? Request { get; private set; }
    public bool IsUncertain { get; private set; }

    public PendingLearningCreation(IdentitySession session)
    {
        this.session = session;
        generation = session.Generation;
        identity = CurrentIdentity();
        session.Changed += SessionChanged;
    }

    public T Begin(T request) => Request ??= request;
    public void MarkUncertain() { if (Request is not null) IsUncertain = true; }
    public void Fail(int status)
    {
        if (status >= 400 && status < 500 && status != 429 && !IsUncertain) Clear();
        else MarkUncertain();
    }
    public void Complete(T request) { if (ReferenceEquals(Request, request)) Clear(); }
    private (int?, int?, string?) CurrentIdentity() => (session.Account?.Id, session.Account?.InstitutionId, session.Account?.Role);
    private void SessionChanged()
    {
        var current = CurrentIdentity();
        if (generation != session.Generation || identity != current) Clear();
        generation = session.Generation;
        identity = current;
    }
    private void Clear() { Request = null; IsUncertain = false; }
    public void Dispose() => session.Changed -= SessionChanged;
}
