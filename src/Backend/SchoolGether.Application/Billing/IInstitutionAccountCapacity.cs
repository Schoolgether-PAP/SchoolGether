namespace SchoolGether.Application.Billing;

public interface IInstitutionAccountCapacity
{
    // The caller owns the account transaction. Future account deletion/restoration must
    // release/reserve capacity in that same transaction; deactivation keeps the seat.
    Task ReserveAsync(int institutionId, CancellationToken cancellationToken);
}
