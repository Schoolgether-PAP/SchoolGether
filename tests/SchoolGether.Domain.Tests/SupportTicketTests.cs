using SchoolGether.Domain.Support;

namespace SchoolGether.Domain.Tests;

public sealed class SupportTicketTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static SupportTicket Open(string subject = "Pedido") => SupportTicket.Open(2, 7, "Docente", Guid.NewGuid(), subject, SupportCategory.Technical, SupportPriority.Normal, Now);
    private static SupportMessage Reply(int institutionId = 2, int ticketId = 0, bool internalMessage = false) => SupportMessage.Create(institutionId, ticketId, 7, "Docente", Guid.NewGuid(), "Resposta", internalMessage, 0, "fingerprint", Now);

    [Fact]
    public void OpenCapturesIdentityAndAuditWithoutReadingClock()
    {
        var clientId = Guid.NewGuid();
        var ticket = SupportTicket.Open(2, 7, "Docente", clientId, "Pedido", SupportCategory.Academic, SupportPriority.High, Now);
        Assert.Equal(2, ticket.InstitutionId);
        Assert.Equal(7, ticket.AuthorId);
        Assert.Equal("Docente", ticket.AuthorName);
        Assert.Equal(clientId, ticket.ClientId);
        Assert.Equal(SupportCategory.Academic, ticket.Category);
        Assert.Equal(SupportPriority.High, ticket.Priority);
        Assert.Equal(SupportStatus.Open, ticket.Status);
        Assert.Equal(1, ticket.RowVersion);
        Assert.Equal(Now, ticket.CreatedAt);
        Assert.Equal(7, ticket.CreatedBy);
        Assert.Null(ticket.UpdatedAt);
    }

    [Fact]
    public void ClosedTicketRejectsEveryMutation()
    {
        var ticket = Open();
        ticket.Close(9, "Resolvido", Now.AddMinutes(1));
        Action[] changes = [
            () => ticket.AddMessage(Reply(), true, Now.AddMinutes(2)),
            () => ticket.ChangeAssignment(9, Now.AddMinutes(2)),
            () => ticket.ChangeStatus(SupportStatus.Open, Now.AddMinutes(2)),
            () => ticket.ChangeStatus(SupportStatus.Closed, Now.AddMinutes(2)),
            () => ticket.ChangePriority(SupportPriority.High, Now.AddMinutes(2)),
            () => ticket.Close(9, "Outro resumo", Now.AddMinutes(2))];
        foreach (var change in changes) Assert.Throws<SupportRuleException>(change);
        Assert.Equal(2, ticket.RowVersion);
        Assert.Equal(SupportStatus.Closed, ticket.Status);
        Assert.Equal("Resolvido", ticket.CloseSummary);
        Assert.Empty(ticket.Messages);
    }

    [Fact]
    public void CustomerReplyReopensAwaitingClient()
    {
        var ticket = Open();
        ticket.ChangeStatus(SupportStatus.AwaitingClient, Now);
        ticket.AddMessage(Reply(), true, Now.AddMinutes(1));
        Assert.Equal(SupportStatus.Open, ticket.Status);
        Assert.Equal(3, ticket.RowVersion);
        Assert.Single(ticket.Messages);
    }

    [Theory]
    [InlineData(SupportStatus.Open)]
    [InlineData(SupportStatus.InProgress)]
    [InlineData(SupportStatus.AwaitingClient)]
    public void StaffReplyPreservesActiveStatus(SupportStatus status)
    {
        var ticket = Open();
        ticket.ChangeStatus(status, Now);
        ticket.AddMessage(Reply(), false, Now.AddMinutes(1));
        Assert.Equal(status, ticket.Status);
    }

    [Fact]
    public void InternalReplyDoesNotReopenAwaitingClient()
    {
        var ticket = Open();
        ticket.ChangeStatus(SupportStatus.AwaitingClient, Now);
        ticket.AddMessage(Reply(internalMessage: true), true, Now.AddMinutes(1));
        Assert.Equal(SupportStatus.AwaitingClient, ticket.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CloseRequiresSummary(string? summary)
    {
        var ticket = Open();
        Assert.Throws<SupportRuleException>(() => ticket.Close(9, summary!, Now));
        Assert.Equal(SupportStatus.Open, ticket.Status);
        Assert.Equal(1, ticket.RowVersion);
        Assert.Null(ticket.ClosedAt);
    }

    [Fact]
    public void CloseAccepts1000CharactersAndRejects1001WithoutMutation()
    {
        var ticket = Open();
        Assert.Throws<SupportRuleException>(() => ticket.Close(9, new string('a', 1001), Now));
        Assert.Equal(1, ticket.RowVersion);
        ticket.Close(9, new string('a', 1000), Now.AddMinutes(1));
        Assert.Equal(1000, ticket.CloseSummary!.Length);
        Assert.Equal(9, ticket.ClosedBy);
        Assert.Equal(Now.AddMinutes(1), ticket.ClosedAt);
    }

    [Fact]
    public void ClosedStatusRequiresCloseOperation()
    {
        var ticket = Open();
        Assert.Throws<SupportRuleException>(() => ticket.ChangeStatus(SupportStatus.Closed, Now));
        Assert.Equal(SupportStatus.Open, ticket.Status);
        Assert.Equal(1, ticket.RowVersion);
    }

    [Fact]
    public void EveryMutationAdvancesVersion()
    {
        var ticket = Open();
        ticket.ChangeAssignment(9, Now.AddMinutes(1));
        Assert.Equal(2, ticket.RowVersion);
        Assert.Equal(9, ticket.AssigneeId);
        ticket.ChangePriority(SupportPriority.High, Now.AddMinutes(2));
        Assert.Equal(3, ticket.RowVersion);
        Assert.Equal(SupportPriority.High, ticket.Priority);
        ticket.ChangeStatus(SupportStatus.InProgress, Now.AddMinutes(3));
        Assert.Equal(4, ticket.RowVersion);
        var message = Reply();
        ticket.AddMessage(message, false, Now.AddMinutes(4));
        Assert.Equal(5, ticket.RowVersion);
        Assert.Same(message, Assert.Single(ticket.Messages));
        ticket.ChangeAssignment(null, Now.AddMinutes(5));
        Assert.Equal(6, ticket.RowVersion);
        Assert.Null(ticket.AssigneeId);
        ticket.Close(9, "Resolvido", Now.AddMinutes(6));
        Assert.Equal(7, ticket.RowVersion);
        Assert.Equal(Now.AddMinutes(6), ticket.UpdatedAt);
        Assert.Equal(Now, ticket.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SubjectIsRequired(string? subject) => Assert.Throws<SupportRuleException>(() => Open(subject!));

    [Fact]
    public void SubjectAccepts150CharactersAndRejects151()
    {
        Assert.Equal(150, Open(new string('a', 150)).Subject.Length);
        Assert.Throws<SupportRuleException>(() => Open(new string('a', 151)));
    }

    [Fact]
    public void ForeignInstitutionAndForeignTicketMessagesAreRejectedWithoutMutation()
    {
        var ticket = Open();
        Assert.Throws<SupportRuleException>(() => ticket.AddMessage(Reply(institutionId: 3), true, Now));
        Assert.Throws<SupportRuleException>(() => ticket.AddMessage(Reply(ticketId: 99), true, Now));
        Assert.Empty(ticket.Messages);
        Assert.Equal(1, ticket.RowVersion);
    }

    [Fact]
    public void DuplicateMessageDoesNotAdvanceVersionOrDuplicateHistory()
    {
        var ticket = Open();
        var message = Reply();
        ticket.AddMessage(message, true, Now);
        Assert.Throws<SupportRuleException>(() => ticket.AddMessage(message, true, Now));
        Assert.Single(ticket.Messages);
        Assert.Equal(2, ticket.RowVersion);
    }

    [Fact]
    public void InvalidMutationLeavesVersionAndAuditUntouched()
    {
        var ticket = Open();
        Assert.Throws<SupportRuleException>(() => ticket.ChangeStatus((SupportStatus)99, Now));
        Assert.Throws<SupportRuleException>(() => ticket.ChangePriority((SupportPriority)99, Now));
        Assert.Throws<SupportRuleException>(() => ticket.ChangeAssignment(0, Now));
        Assert.Throws<SupportRuleException>(() => ticket.Close(0, "Resolvido", Now));
        Assert.Equal(1, ticket.RowVersion);
        Assert.Null(ticket.UpdatedAt);
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(2, 0)]
    public void OpenRequiresPositiveIdentifiers(int institutionId, int authorId) =>
        Assert.Throws<SupportRuleException>(() => SupportTicket.Open(institutionId, authorId, "Docente", Guid.NewGuid(), "Pedido", SupportCategory.Other, SupportPriority.Low, Now));

    [Fact]
    public void OpenRejectsInvalidMetadata()
    {
        Assert.Throws<SupportRuleException>(() => SupportTicket.Open(2, 7, " ", Guid.NewGuid(), "Pedido", SupportCategory.Other, SupportPriority.Low, Now));
        Assert.Throws<SupportRuleException>(() => SupportTicket.Open(2, 7, "Docente", Guid.Empty, "Pedido", SupportCategory.Other, SupportPriority.Low, Now));
        Assert.Throws<SupportRuleException>(() => SupportTicket.Open(2, 7, "Docente", Guid.NewGuid(), "Pedido", (SupportCategory)99, SupportPriority.Low, Now));
        Assert.Throws<SupportRuleException>(() => SupportTicket.Open(2, 7, "Docente", Guid.NewGuid(), "Pedido", SupportCategory.Other, (SupportPriority)99, Now));
    }

    [Fact]
    public void NonUtcMutationIsRejectedBeforeStateChanges()
    {
        var ticket = Open();
        Assert.Throws<SupportRuleException>(() => ticket.ChangePriority(SupportPriority.High, DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.Equal(1, ticket.RowVersion);
        Assert.Equal(SupportPriority.Normal, ticket.Priority);
    }
}
