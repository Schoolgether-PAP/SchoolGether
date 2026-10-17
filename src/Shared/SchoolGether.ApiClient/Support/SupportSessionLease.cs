using SchoolGether.ApiClient.Identity;

namespace SchoolGether.ApiClient.Support;

// Cada operação tem o seu cancelamento; atualizar o perfil na mesma sessão não o invalida.
internal sealed class SupportSessionLease : IDisposable
{
    private readonly IdentitySession session;
    private readonly long generation;
    private readonly CancellationTokenSource cancellation;
    private readonly object gate = new();
    private bool disposed;
    public CancellationToken Token { get; }
    public SupportSessionLease(IdentitySession session, CancellationToken ct)
    {
        this.session = session;
        generation = session.Generation;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Token = cancellation.Token;
        session.Changed += SessionChanged;
        SessionChanged();
    }
    private void SessionChanged()
    {
        lock (gate) { if (!disposed && session.Generation != generation) cancellation.Cancel(); }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true; session.Changed -= SessionChanged;
            cancellation.Cancel(); cancellation.Dispose();
        }
    }
}
