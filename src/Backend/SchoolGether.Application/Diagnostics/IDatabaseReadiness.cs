namespace SchoolGether.Application.Diagnostics;

public interface IDatabaseReadiness
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}
