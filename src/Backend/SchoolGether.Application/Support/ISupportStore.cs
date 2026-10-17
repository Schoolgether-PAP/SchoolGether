using SchoolGether.Domain.Support;

namespace SchoolGether.Application.Support;

public interface ISupportStore
{
    Task<SupportTicketPage> ListAsync(AuthorizedSupportScope scope, SupportTicketQuery query, CancellationToken ct);
    Task<SupportTicket?> FindTicketAsync(AuthorizedSupportScope scope, int id, CancellationToken ct);
    Task<SupportMessagePage> ListMessagesAsync(AuthorizedSupportScope scope, int ticketId, int? beforeId, int? afterId, int take, CancellationToken ct);
    Task<SupportMessage?> FindMessageByClientIdAsync(AuthorizedSupportScope scope, int ticketId, Guid clientId, CancellationToken ct);
    Task<SupportTicket?> FindTicketByClientIdAsync(AuthorizedSupportScope scope, Guid clientId, CancellationToken ct);
    Task<SupportAttachment?> FindAttachmentAsync(AuthorizedSupportScope scope, int id, CancellationToken ct);
    void AddTicket(SupportTicket ticket);
    void AddMessage(SupportMessage message);
    Task ReserveBytesAsync(AuthorizedSupportScope scope, long bytes, long limit, CancellationToken ct);
}
