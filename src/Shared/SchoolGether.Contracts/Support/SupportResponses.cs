namespace SchoolGether.Contracts.Support;

public sealed record SupportTicketResponse(int Id, int InstitutionId, int AuthorId, string AuthorName,
    string Subject, string Category, string Priority, string Status, int? AssigneeId, string? AssigneeName,
    long RowVersion, DateTime CreatedAt, DateTime? ClosedAt, int? ClosedBy, string? CloseSummary,
    bool CanReply, bool CanManage, bool CanClose);

public sealed record SupportAttachmentResponse(int Id, string FileName, string ContentType, long Size);

public sealed record SupportMessageResponse(int Id, Guid ClientId, int AuthorId, string AuthorName,
    string Text, DateTime CreatedAt, IReadOnlyList<SupportAttachmentResponse> Attachments);

public sealed record SupportAdminMessageResponse(int Id, Guid ClientId, int AuthorId, string AuthorName,
    string Text, DateTime CreatedAt, IReadOnlyList<SupportAttachmentResponse> Attachments, bool Internal);

public sealed record SupportContextResponse(bool Enabled, bool CanOpen, bool CanReadGlobal, bool CanManageGlobal);
public sealed record SupportAssigneeResponse(int Id, string Name);
public sealed record SupportTicketPageResponse(IReadOnlyList<SupportTicketResponse> Items, int Total, int Page, int PageSize);
public sealed record SupportMessagePageResponse(IReadOnlyList<SupportMessageResponse> Items, bool HasMore);
public sealed record SupportAdminMessagePageResponse(IReadOnlyList<SupportAdminMessageResponse> Items, bool HasMore);
