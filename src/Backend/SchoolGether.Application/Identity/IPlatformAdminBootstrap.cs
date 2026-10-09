namespace SchoolGether.Application.Identity;

public interface IPlatformAdminBootstrap
{
    Task<bool> CreateAsync(string email, string password, CancellationToken cancellationToken);
}
