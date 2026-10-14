namespace SchoolGether.Application.Onboarding;

public interface IOnboardingUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
