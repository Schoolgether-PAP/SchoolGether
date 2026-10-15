namespace SchoolGether.Contracts.Support;

public sealed record SupportChangedEvent(int TicketId, long RowVersion);
