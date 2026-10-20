namespace SchoolGether.Application.Support;

public interface ISupportUnitOfWork
{
    Task<T> ExecuteAsync<T>(AuthorizedSupportScope scope, Func<CancellationToken, Task<T>> operation, CancellationToken ct);
}
