using SchoolGether.ApiClient.Identity;

namespace SchoolGether.Mobile.Identity;

public sealed class SecureRefreshTokenStore : IRefreshTokenStore
{
    private const string Key = "schoolgether.refresh-token";

    public async Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { return await SecureStorage.Default.GetAsync(Key); }
        catch (Exception) { throw new IOException("ProtectedStorageUnavailable"); }
    }

    public async Task WriteAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (refreshToken is null) SecureStorage.Default.Remove(Key);
            else await SecureStorage.Default.SetAsync(Key, refreshToken);
        }
        catch (Exception) { throw new IOException("ProtectedStorageUnavailable"); }
    }
}
