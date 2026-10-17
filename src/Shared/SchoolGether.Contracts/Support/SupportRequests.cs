using System.Text.Json.Serialization;

namespace SchoolGether.Contracts.Support;

// Os ficheiros são partes multipart separadas; estes contratos transportam só os metadados do pedido.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateSupportTicketRequest(Guid ClientId, string Subject, string Category, string Priority, string Description);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SendSupportMessageRequest(Guid ClientId, long RowVersion, string Text, bool Internal);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateSupportTicketRequest(long RowVersion, string Status, string Priority, int? AssigneeId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CloseSupportTicketRequest(long RowVersion, string Summary);
