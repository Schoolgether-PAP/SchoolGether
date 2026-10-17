namespace SchoolGether.Domain.Support;

public sealed class SupportTicket
{
    private readonly List<SupportMessage> _messages = [];
    private SupportTicket() { }

    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public int AuthorId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public Guid ClientId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public SupportCategory Category { get; private set; }
    public SupportPriority Priority { get; private set; }
    public SupportStatus Status { get; private set; }
    public int? AssigneeId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTime? ClosedAt { get; private set; }
    public int? ClosedBy { get; private set; }
    public string? CloseSummary { get; private set; }
    public IReadOnlyCollection<SupportMessage> Messages => _messages.AsReadOnly();

    public static SupportTicket Open(int institutionId, int authorId, string authorName, Guid clientId,
        string subject, SupportCategory category, SupportPriority priority, DateTime now)
    {
        if (institutionId <= 0 || authorId <= 0 || clientId == Guid.Empty || string.IsNullOrWhiteSpace(authorName))
            throw new SupportRuleException("InvalidTicketIdentity");
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 150)
            throw new SupportRuleException("InvalidSubject");
        if (!Enum.IsDefined(category) || !Enum.IsDefined(priority))
            throw new SupportRuleException("InvalidTicketClassification");
        RequireUtc(now);
        return new SupportTicket
        {
            InstitutionId = institutionId, AuthorId = authorId, AuthorName = authorName,
            ClientId = clientId, Subject = subject.Trim(), Category = category, Priority = priority,
            Status = SupportStatus.Open, CreatedAt = now, CreatedBy = authorId
        };
    }

    public void AddMessage(SupportMessage message, bool customerMessage, DateTime now)
    {
        EnsureMutable(now);
        if (message is null || message.InstitutionId != InstitutionId || message.TicketId != Id)
            throw new SupportRuleException("MessageOutsideTicket");
        if (_messages.Any(existing => existing.ClientId == message.ClientId))
            throw new SupportRuleException("DuplicateMessage");
        _messages.Add(message);
        if (customerMessage && !message.Internal && Status == SupportStatus.AwaitingClient)
            Status = SupportStatus.Open;
        AdvanceVersion(now);
    }

    public void ChangeAssignment(int? assigneeId, DateTime now)
    {
        EnsureMutable(now);
        if (assigneeId is <= 0) throw new SupportRuleException("InvalidAssignee");
        AssigneeId = assigneeId;
        AdvanceVersion(now);
    }

    public void ChangeStatus(SupportStatus status, DateTime now)
    {
        EnsureMutable(now);
        if (!Enum.IsDefined(status) || status == SupportStatus.Closed)
            throw new SupportRuleException("InvalidStatusTransition");
        Status = status;
        AdvanceVersion(now);
    }

    public void ChangePriority(SupportPriority priority, DateTime now)
    {
        EnsureMutable(now);
        if (!Enum.IsDefined(priority)) throw new SupportRuleException("InvalidPriority");
        Priority = priority;
        AdvanceVersion(now);
    }

    public void Close(int actorId, string summary, DateTime now)
    {
        EnsureMutable(now);
        if (actorId <= 0) throw new SupportRuleException("InvalidClosingActor");
        if (string.IsNullOrWhiteSpace(summary) || summary.Length > 1000)
            throw new SupportRuleException("InvalidCloseSummary");
        Status = SupportStatus.Closed;
        ClosedBy = actorId;
        ClosedAt = now;
        CloseSummary = summary.Trim();
        AdvanceVersion(now);
    }

    private void EnsureMutable(DateTime now)
    {
        if (Status == SupportStatus.Closed) throw new SupportRuleException("TicketClosed");
        RequireUtc(now);
    }

    private static void RequireUtc(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc) throw new SupportRuleException("UtcDateRequired");
    }

    private void AdvanceVersion(DateTime now)
    {
        RowVersion++;
        UpdatedAt = now;
    }
}
