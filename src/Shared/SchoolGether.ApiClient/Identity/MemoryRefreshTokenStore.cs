namespace SchoolGether.ApiClient.Identity;

public sealed class MemoryRefreshTokenStore : IRefreshTokenStore
{
    private string? refreshToken;

    public Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Volatile.Read(ref refreshToken));
    }

    public Task WriteAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref this.refreshToken, refreshToken);
        return Task.CompletedTask;
    }
}
