namespace SchoolGether.Domain.Support;

public sealed class SupportAttachment
{
    private SupportAttachment() { }

    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public int MessageId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public long RowVersion { get; private set; } = 1;

    public static SupportAttachment Create(int institutionId, int messageId, string storageKey, string fileName,
        string contentType, long size, string sha256, int createdBy, DateTime now)
    {
        if (institutionId <= 0 || messageId < 0 || createdBy <= 0)
            throw new SupportRuleException("InvalidAttachmentIdentity");
        if (string.IsNullOrWhiteSpace(storageKey) || string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentType))
            throw new SupportRuleException("InvalidAttachmentMetadata");
        if (size is <= 0 or > 5242880) throw new SupportRuleException("InvalidAttachmentSize");
        if (sha256 is null || sha256.Length != 64 || sha256.Any(character => !Uri.IsHexDigit(character)))
            throw new SupportRuleException("InvalidAttachmentDigest");
        if (now.Kind != DateTimeKind.Utc) throw new SupportRuleException("UtcDateRequired");
        return new SupportAttachment
        {
            InstitutionId = institutionId, MessageId = messageId, StorageKey = storageKey,
            FileName = fileName, ContentType = contentType, Size = size, Sha256 = sha256,
            CreatedBy = createdBy, CreatedAt = now
        };
    }
}

