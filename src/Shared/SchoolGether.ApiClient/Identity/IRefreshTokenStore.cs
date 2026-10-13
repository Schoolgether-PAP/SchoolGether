namespace SchoolGether.ApiClient.Identity;

public interface IRefreshTokenStore
{
    Task<string?> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(string? refreshToken, CancellationToken cancellationToken);
}
