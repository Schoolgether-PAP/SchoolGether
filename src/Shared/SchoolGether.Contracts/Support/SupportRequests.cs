namespace SchoolGether.Contracts.Support;

// Os ficheiros são partes multipart separadas; estes contratos transportam só os metadados do pedido.
public sealed record CreateSupportTicketRequest(Guid ClientId, string Subject, string Category, string Priority, string Description);
public sealed record SendSupportMessageRequest(Guid ClientId, long RowVersion, string Text, bool Internal);
public sealed record UpdateSupportTicketRequest(long RowVersion, string Status, string Priority, int? AssigneeId);
public sealed record CloseSupportTicketRequest(long RowVersion, string Summary);
