namespace SchoolGether.Application.Identity;

// Apenas agregados e responsáveis, sem credenciais nem acesso escolar arbitrário.
public interface IPlatformAccountReader
{
    Task<IReadOnlyDictionary<int, int>> CountByInstitutionsAsync(IReadOnlyList<int> institutionIds, CancellationToken cancellationToken);
    Task<int> CountAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Account>> ListAdministratorsAsync(int institutionId, CancellationToken cancellationToken);
}

public interface IInstitutionAdministratorProvisioner
{
    Task<Account> CreateAsync(int institutionId, string email, string password, string name, int createdBy, CancellationToken cancellationToken);
}
