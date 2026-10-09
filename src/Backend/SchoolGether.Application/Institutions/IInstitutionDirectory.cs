namespace SchoolGether.Application.Institutions;

public interface IInstitutionDirectory
{
    Task<bool> ExistsAsync(int institutionId, CancellationToken cancellationToken);
    Task<int> CreateAsync(string name, int createdBy, CancellationToken cancellationToken);
}
