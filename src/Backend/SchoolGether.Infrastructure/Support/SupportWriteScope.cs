using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SchoolGether.Application.Support;
using SchoolGether.Domain.Audit;
using SchoolGether.Domain.Support;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Support;

// Capacidade confinada à transação e às referências exatas do agregado autorizado.
internal sealed class SupportWriteScope(SchoolGetherDbContext db, AuthorizedSupportScope authorized)
{
    private readonly Guid transactionId = db.Database.CurrentTransaction?.TransactionId ?? throw new InvalidOperationException("SupportTransactionRequired");
    private SupportTicket? ticket;
    private readonly HashSet<SupportMessage> messages = new(ReferenceEqualityComparer.Instance);
    internal void BindTicket(SupportTicket value)
    {
        if (ticket is not null && !ReferenceEquals(ticket, value)) throw new InvalidOperationException("SupportWriteDenied");
        if (authorized.Operation == "read" || (authorized.TicketId.HasValue && value.Id != authorized.TicketId) ||
            (!authorized.Global && (value.InstitutionId != authorized.Actor.InstitutionId ||
             (authorized.Actor.Role == "Teacher" && value.AuthorId != authorized.Actor.Id))))
            throw new InvalidOperationException("SupportWriteDenied");
        ticket = value;
    }
    internal void BindMessage(SupportMessage value)
    {
        if (ticket is null || value.TicketId != ticket.Id || value.InstitutionId != ticket.InstitutionId)
            throw new InvalidOperationException("SupportWriteDenied");
        messages.Add(value);
    }
    internal int RequireInstitution(AuthorizedSupportScope scope)
    {
        if (!ReferenceEquals(authorized, scope) || ticket is null || authorized.Operation is not ("create" or "reply") ||
            db.Database.CurrentTransaction?.TransactionId != transactionId) throw new InvalidOperationException("SupportWriteDenied");
        return ticket.InstitutionId;
    }
    internal bool Allows(EntityEntry entry)
    {
        if (db.Database.CurrentTransaction?.TransactionId != transactionId || ticket is null) return false;
        if (entry.Entity is AuditEntry audit)
            return entry.State == EntityState.Added && audit.ActorId == authorized.Actor.Id && audit.InstitutionId == ticket.InstitutionId &&
                audit.Action == "support." + (authorized.Operation == "manage" ? "update" :
                    authorized.Operation == "reply" && messages.Any(x => x.Internal) ? "note" : authorized.Operation);
        if (ReferenceEquals(entry.Entity, ticket))
        {
            if (entry.State == EntityState.Added)
                return authorized.Operation == "create" && ticket.AuthorId == authorized.Actor.Id && ticket.InstitutionId == authorized.Actor.InstitutionId;
            if (entry.State != EntityState.Modified || authorized.Operation is not ("reply" or "manage" or "close")) return false;
            if (authorized.Operation == "close" && (ticket.Status != SupportStatus.Closed || ticket.ClosedBy != authorized.Actor.Id)) return false;
            var allowed = authorized.Operation switch {
                "reply" => new[] { "Status", "RowVersion", "UpdatedAt" },
                "close" => new[] { "Status", "ClosedAt", "ClosedBy", "CloseSummary", "RowVersion", "UpdatedAt" },
                _ => new[] { "Status", "Priority", "AssigneeId", "RowVersion", "UpdatedAt" } };
            return entry.Properties.Where(x => x.IsModified).All(x => allowed.Contains(x.Metadata.Name));
        }
        if (entry.Entity is SupportAttachment attachment)
            return entry.State == EntityState.Added && authorized.Operation is "create" or "reply" &&
                attachment.InstitutionId == ticket.InstitutionId && attachment.CreatedBy == authorized.Actor.Id &&
                messages.Any(m => m.Attachments.Contains(attachment) && m.Id == attachment.MessageId && (!m.Internal || authorized.Actor.CanManageGlobal));
        return entry.Entity is SupportMessage message && messages.Contains(message) && entry.State == EntityState.Added &&
            authorized.Operation is "create" or "reply" && message.AuthorId == authorized.Actor.Id &&
            message.CreatedBy == authorized.Actor.Id && message.InstitutionId == ticket.InstitutionId && message.TicketId == ticket.Id &&
            (!message.Internal || authorized.Actor.CanManageGlobal);
    }
}
