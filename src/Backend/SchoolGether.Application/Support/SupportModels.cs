using SchoolGether.Domain.Support;

namespace SchoolGether.Application.Support;

public sealed record SupportActor(int Id, string Name, string Role, int? InstitutionId, bool CanReadGlobal, bool CanManageGlobal);
// Identidade autenticada capturada pelo servidor; não transporta permissões nem um ator autorizado.
public sealed record SupportSession(int Id, int? InstitutionId, string Role);
public sealed class AuthorizedSupportScope
{
    internal AuthorizedSupportScope(SupportActor actor, string operation, int? ticketId = null)
    { Actor = actor; Operation = operation; TicketId = ticketId; }
    public SupportActor Actor { get; }
    public string Operation { get; }
    public int? TicketId { get; }
    public bool Global => Actor.CanReadGlobal;
    public bool IncludeInternal => Global;
}

public sealed record SupportTicketQuery(int Page = 1, int PageSize = 20, string? Search = null,
    string? Status = null, string? Priority = null, int? InstitutionId = null, int? AssigneeId = null);
public sealed record IncomingAttachment(string FileName, Stream Content);
public sealed record PreparedAttachment(string StorageKey, string FileName, string ContentType, long Size, string Sha256);
public sealed record CreateSupportTicket(Guid ClientId, string Subject, string Category, string Priority,
    string Description, IReadOnlyList<IncomingAttachment> Files);
public sealed record SendSupportMessage(Guid ClientId, long RowVersion, string Text, bool Internal, IReadOnlyList<IncomingAttachment> Files);
public sealed record UpdateSupportTicket(long RowVersion, string Status, string Priority, int? AssigneeId);
public sealed record CloseSupportTicket(long RowVersion, string Summary);
public sealed record SupportTicketResult(int Id, int InstitutionId, int AuthorId, string AuthorName,
    string Subject, string Category, string Priority, string Status, int? AssigneeId, string? AssigneeName,
    long RowVersion, DateTime CreatedAt, DateTime? ClosedAt, int? ClosedBy, string? CloseSummary,
    bool CanReply, bool CanManage, bool CanClose, bool Replay = false, bool AssigneeAvailable = true);
public sealed record SupportAttachmentResult(int Id, string FileName, string ContentType, long Size);
public sealed record SupportMessageResult(int Id, Guid ClientId, int AuthorId, string AuthorName, string Text,
    DateTime CreatedAt, IReadOnlyList<SupportAttachmentResult> Attachments, bool Internal, bool Replay = false);
public sealed record SupportTicketPage(IReadOnlyList<SupportTicketResult> Items, int Total, int Page, int PageSize);
public sealed record SupportMessagePage(IReadOnlyList<SupportMessageResult> PublicMessages,
    IReadOnlyList<SupportMessageResult> InternalMessages, bool HasMore, bool Administrative);
public sealed record SupportAssignee(int Id, string Name);
public sealed record SupportContext(bool Enabled, bool CanOpen, bool CanReadGlobal, bool CanManageGlobal);
public sealed record SupportDownload(Stream Content, string FileName, string ContentType);
public sealed class SupportException(string code) : Exception(code) { public string Code { get; } = code; }
public sealed class SupportOptions { public bool Enabled { get; set; } public string FilesRoot { get; set; } = string.Empty; public long InstitutionStorageLimitBytes { get; set; } = 104857600; }

public static class SupportProjection
{
    public static SupportTicketResult Ticket(SupportTicket ticket, AuthorizedSupportScope scope, string? assigneeName = null, bool replay = false,
        bool assigneeAvailable = true)
    {
        var open = ticket.Status != SupportStatus.Closed;
        return new(ticket.Id, ticket.InstitutionId, ticket.AuthorId, ticket.AuthorName, ticket.Subject,
            ticket.Category.ToString(), ticket.Priority.ToString(), ticket.Status.ToString(), ticket.AssigneeId, assigneeName,
            ticket.RowVersion, ticket.CreatedAt, ticket.ClosedAt, ticket.ClosedBy, ticket.CloseSummary,
            open && (!scope.Global || scope.Actor.CanManageGlobal), open && scope.Actor.CanManageGlobal,
            open && (!scope.Global || scope.Actor.CanManageGlobal), replay, assigneeAvailable);
    }
    public static SupportMessageResult Message(SupportMessage message, bool replay = false) =>
        new(message.Id, message.ClientId, message.AuthorId, message.AuthorName, message.Text, message.CreatedAt,
            message.Attachments.Select(x => new SupportAttachmentResult(x.Id, x.FileName, x.ContentType, x.Size)).ToArray(), message.Internal, replay);
}
