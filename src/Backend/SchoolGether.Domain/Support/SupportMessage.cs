namespace SchoolGether.Domain.Support;

public sealed class SupportMessage
{
    private readonly List<SupportAttachment> _attachments = [];
    private SupportMessage() { }

    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public int TicketId { get; private set; }
    public int AuthorId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public Guid ClientId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool Internal { get; private set; }
    public int AttachmentsCount { get; private set; }
    public string Fingerprint { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public IReadOnlyCollection<SupportAttachment> Attachments => _attachments.AsReadOnly();

    public static SupportMessage Create(int institutionId, int ticketId, int authorId, string authorName,
        Guid clientId, string text, bool internalMessage, int attachmentsCount, string fingerprint, DateTime now)
    {
        if (institutionId <= 0 || ticketId < 0 || authorId <= 0 || clientId == Guid.Empty || string.IsNullOrWhiteSpace(authorName))
            throw new SupportRuleException("InvalidMessageIdentity");
        if (attachmentsCount is < 0 or > 3) throw new SupportRuleException("InvalidAttachmentCount");
        if ((string.IsNullOrWhiteSpace(text) && attachmentsCount == 0) || text?.Length > 4000)
            throw new SupportRuleException("InvalidMessageText");
        if (string.IsNullOrWhiteSpace(fingerprint)) throw new SupportRuleException("FingerprintRequired");
        if (now.Kind != DateTimeKind.Utc) throw new SupportRuleException("UtcDateRequired");
        return new SupportMessage
        {
            InstitutionId = institutionId, TicketId = ticketId, AuthorId = authorId, AuthorName = authorName,
            ClientId = clientId, Text = text ?? string.Empty, Internal = internalMessage,
            AttachmentsCount = attachmentsCount, Fingerprint = fingerprint, CreatedAt = now, CreatedBy = authorId
        };
    }

    public static SupportMessage CreateInitial(int institutionId, int ticketId, int authorId, string authorName,
        Guid clientId, string text, int attachmentsCount, string fingerprint, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new SupportRuleException("InitialDescriptionRequired");
        return Create(institutionId, ticketId, authorId, authorName, clientId, text, false, attachmentsCount, fingerprint, now);
    }
}
