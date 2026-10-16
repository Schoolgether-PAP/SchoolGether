namespace SchoolGether.Application.Support;

public interface ISupportNotifier
{
    Task ChangedAsync(int ticketId, bool internalOnly, CancellationToken ct);
}
