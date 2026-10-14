namespace SchoolGether.Application.Institutions;

public interface IInstitutionSetupStore
{
    Task<InstitutionSetup> GetAsync(int institutionId, CancellationToken cancellationToken);
    Task<InstitutionSetup> SaveAsync(int institutionId, int actorId, SaveInstitutionSetup command, DateTime completedAt,
        CancellationToken cancellationToken);
}
