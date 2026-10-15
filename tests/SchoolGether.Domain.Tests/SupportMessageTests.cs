using SchoolGether.Domain.Support;

namespace SchoolGether.Domain.Tests;

public sealed class SupportMessageTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static SupportMessage Create(string text = "Resposta", int attachments = 0) => SupportMessage.Create(2, 5, 7, "Docente", Guid.NewGuid(), text, false, attachments, "fingerprint", Now);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InitialDescriptionIsRequired(string? description)
    {
        Assert.Throws<SupportRuleException>(() => SupportMessage.CreateInitial(2, 0, 7, "Docente", Guid.NewGuid(), description!, 1, "fingerprint", Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankReplyRequiresAttachment(string? text)
    {
        Assert.Throws<SupportRuleException>(() => Create(text!, 0));
        Assert.Equal(1, Create(text!, 1).AttachmentsCount);
    }

    [Fact]
    public void TextAccepts4000CharactersAndRejects4001()
    {
        Assert.Equal(4000, Create(new string('a', 4000)).Text.Length);
        Assert.Throws<SupportRuleException>(() => Create(new string('a', 4001), 1));
        Assert.Equal(4000, SupportMessage.CreateInitial(2, 0, 7, "Docente", Guid.NewGuid(), new string('a', 4000), 0, "fingerprint", Now).Text.Length);
        Assert.Throws<SupportRuleException>(() => SupportMessage.CreateInitial(2, 0, 7, "Docente", Guid.NewGuid(), new string('a', 4001), 1, "fingerprint", Now));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void AttachmentCountMustBeBetweenZeroAndThree(int attachments) => Assert.Throws<SupportRuleException>(() => Create(attachments: attachments));

    [Fact]
    public void ThreeAttachmentsAreAccepted() => Assert.Equal(3, Create(attachments: 3).AttachmentsCount);

    [Fact]
    public void MessagePreservesContentIdentityAndUtcAudit()
    {
        var clientId = Guid.NewGuid();
        var message = SupportMessage.Create(2, 5, 9, "Responsável", clientId, "  Texto original  ", true, 2, "fingerprint", Now);
        Assert.Equal(2, message.InstitutionId);
        Assert.Equal(5, message.TicketId);
        Assert.Equal(9, message.AuthorId);
        Assert.Equal("Responsável", message.AuthorName);
        Assert.Equal(clientId, message.ClientId);
        Assert.Equal("  Texto original  ", message.Text);
        Assert.True(message.Internal);
        Assert.Equal(2, message.AttachmentsCount);
        Assert.Equal("fingerprint", message.Fingerprint);
        Assert.Equal(Now, message.CreatedAt);
        Assert.Equal(9, message.CreatedBy);
        Assert.Equal(1, message.RowVersion);
    }

    [Fact]
    public void InitialMessageIsAlwaysPublic() => Assert.False(SupportMessage.CreateInitial(2, 0, 7, "Docente", Guid.NewGuid(), "Descrição", 0, "fingerprint", Now).Internal);

    [Fact]
    public void MessageRejectsInvalidMetadata()
    {
        Assert.Throws<SupportRuleException>(() => SupportMessage.Create(0, 5, 7, "Docente", Guid.NewGuid(), "Texto", false, 0, "fingerprint", Now));
        Assert.Throws<SupportRuleException>(() => SupportMessage.Create(2, -1, 7, "Docente", Guid.NewGuid(), "Texto", false, 0, "fingerprint", Now));
        Assert.Throws<SupportRuleException>(() => SupportMessage.Create(2, 5, 0, "Docente", Guid.NewGuid(), "Texto", false, 0, "fingerprint", Now));
        Assert.Throws<SupportRuleException>(() => SupportMessage.Create(2, 5, 7, " ", Guid.NewGuid(), "Texto", false, 0, "fingerprint", Now));
        Assert.Throws<SupportRuleException>(() => SupportMessage.Create(2, 5, 7, "Docente", Guid.Empty, "Texto", false, 0, "fingerprint", Now));
        Assert.Throws<SupportRuleException>(() => SupportMessage.Create(2, 5, 7, "Docente", Guid.NewGuid(), "Texto", false, 0, " ", Now));
        Assert.Throws<SupportRuleException>(() => SupportMessage.Create(2, 5, 7, "Docente", Guid.NewGuid(), "Texto", false, 0, "fingerprint", DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }
}

public sealed class SupportAttachmentTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static SupportAttachment Create(long size = 12) => SupportAttachment.Create(2, 5, "opaque-key", "anexo.pdf", "application/pdf", size, new string('a', 64), 7, Now);

    [Fact]
    public void AttachmentCapturesMetadataAndAuditWithoutExposingFileAccess()
    {
        var attachment = Create();
        Assert.Equal(2, attachment.InstitutionId);
        Assert.Equal(5, attachment.MessageId);
        Assert.Equal("opaque-key", attachment.StorageKey);
        Assert.Equal("anexo.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(12, attachment.Size);
        Assert.Equal(new string('a', 64), attachment.Sha256);
        Assert.Equal(7, attachment.CreatedBy);
        Assert.Equal(Now, attachment.CreatedAt);
        Assert.Equal(1, attachment.RowVersion);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(5242881)]
    public void AttachmentRejectsSizesOutsideFiveMiBLimit(long size) => Assert.Throws<SupportRuleException>(() => Create(size));

    [Fact]
    public void AttachmentAcceptsExactlyFiveMiB() => Assert.Equal(5242880, Create(5242880).Size);

    [Fact]
    public void UnsavedMessageMayHaveZeroForeignKey() => Assert.Equal(0, SupportAttachment.Create(2, 0, "key", "anexo.pdf", "application/pdf", 1, new string('a', 64), 7, Now).MessageId);

    [Theory]
    [InlineData(0, 5, 7)]
    [InlineData(2, -1, 7)]
    [InlineData(2, 5, 0)]
    public void AttachmentRejectsInvalidIdentifiers(int institutionId, int messageId, int creator) =>
        Assert.Throws<SupportRuleException>(() => SupportAttachment.Create(institutionId, messageId, "key", "anexo.pdf", "application/pdf", 1, new string('a', 64), creator, Now));

    [Theory]
    [InlineData("", "anexo.pdf", "application/pdf")]
    [InlineData("key", " ", "application/pdf")]
    [InlineData("key", "anexo.pdf", "")]
    public void AttachmentRequiresStorageAndDisplayMetadata(string key, string name, string contentType) =>
        Assert.Throws<SupportRuleException>(() => SupportAttachment.Create(2, 5, key, name, contentType, 1, new string('a', 64), 7, Now));

    [Theory]
    [InlineData("")]
    [InlineData("abcd")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void AttachmentRequiresSha256Digest(string digest) =>
        Assert.Throws<SupportRuleException>(() => SupportAttachment.Create(2, 5, "key", "anexo.pdf", "application/pdf", 1, digest, 7, Now));

    [Fact]
    public void AttachmentRejectsNonUtcAudit() => Assert.Throws<SupportRuleException>(() => SupportAttachment.Create(2, 5, "key", "anexo.pdf", "application/pdf", 1, new string('a', 64), 7, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
}
