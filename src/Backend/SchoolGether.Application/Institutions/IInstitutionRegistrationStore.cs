namespace SchoolGether.Application.Institutions;

public interface IInstitutionRegistrationStore
{
    Task<int> CreateAsync(string name, string type, string? locality, string academicYear, int ownerId, CancellationToken cancellationToken);
}
