using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Support;
using SchoolGether.Domain.Support;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Support;

internal sealed class SupportStore(SchoolGetherDbContext db) : ISupportStore
{
    // O único contorno de filtros do módulo reaplica sempre o âmbito emitido pela autorização.
    private IQueryable<SupportTicket> Tickets(AuthorizedSupportScope scope) => db.SupportTickets.IgnoreQueryFilters()
        .Where(x => x.DeletedAt == null && (scope.Global || (x.InstitutionId == scope.Actor.InstitutionId &&
            (scope.Actor.Role != "Teacher" || x.AuthorId == scope.Actor.Id))) && (!scope.TicketId.HasValue || x.Id == scope.TicketId));
    private IQueryable<SupportMessage> Messages(AuthorizedSupportScope scope, int ticketId) => db.SupportMessages.IgnoreQueryFilters()
        .Where(x => x.DeletedAt == null && x.TicketId == ticketId && (scope.IncludeInternal || !x.Internal) &&
            Tickets(scope).Any(t => t.Id == x.TicketId && t.InstitutionId == x.InstitutionId));

    public async Task<SupportTicketPage> ListAsync(AuthorizedSupportScope scope, SupportTicketQuery query, CancellationToken ct)
    {
        var tickets = Tickets(scope).AsNoTracking();
        if (query.Search is not null) tickets = tickets.Where(x => x.Subject.Contains(query.Search));
        if (query.Status is not null) { var status = Enum.Parse<SupportStatus>(query.Status); tickets = tickets.Where(x => x.Status == status); }
        if (query.Priority is not null) { var priority = Enum.Parse<SupportPriority>(query.Priority); tickets = tickets.Where(x => x.Priority == priority); }
        if (query.InstitutionId is int institution) tickets = tickets.Where(x => x.InstitutionId == institution);
        if (query.AssigneeId is int assignee) tickets = tickets.Where(x => x.AssigneeId == assignee);
        var total = await tickets.CountAsync(ct);
        var items = await tickets.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(items.Select(x => SupportProjection.Ticket(x, scope)).ToArray(), total, query.Page, query.PageSize);
    }
    public async Task<SupportTicket?> FindTicketAsync(AuthorizedSupportScope scope, int id, CancellationToken ct)
    {
        var ticket = await Tickets(scope).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is not null) db.SupportScope?.BindTicket(ticket);
        return ticket;
    }
    public async Task<SupportTicket?> FindTicketByClientIdAsync(AuthorizedSupportScope scope, Guid clientId, CancellationToken ct) =>
        await Tickets(scope).SingleOrDefaultAsync(x => x.AuthorId == scope.Actor.Id && x.ClientId == clientId, ct);
    public async Task<SupportMessage?> FindMessageByClientIdAsync(AuthorizedSupportScope scope, int ticketId, Guid clientId, CancellationToken ct) =>
        await Messages(scope, ticketId).AsNoTracking().SingleOrDefaultAsync(x => x.ClientId == clientId, ct);
    public async Task<SupportMessagePage> ListMessagesAsync(AuthorizedSupportScope scope, int ticketId, int? beforeId, int? afterId, int take, CancellationToken ct)
    {
        var query = Messages(scope, ticketId).AsNoTracking();
        if (beforeId is int before) query = query.Where(x => x.Id < before);
        if (afterId is int after) query = query.Where(x => x.Id > after);
        var ordered = afterId.HasValue ? query.OrderBy(x => x.Id) : query.OrderByDescending(x => x.Id);
        var rows = await ordered.Take(take + 1).ToListAsync(ct);
        var items = rows.Take(take).OrderBy(x => x.Id).Select(x => SupportProjection.Message(x)).ToArray();
        return new(items.Where(x => !x.Internal).ToArray(), items.Where(x => x.Internal).ToArray(), rows.Count > take, scope.IncludeInternal);
    }
    public void AddTicket(SupportTicket ticket)
    {
        var scope = db.SupportScope ?? throw new InvalidOperationException("SupportWriteDenied");
        scope.BindTicket(ticket);
        foreach (var message in ticket.Messages) scope.BindMessage(message);
        db.SupportTickets.Add(ticket);
    }
    public void AddMessage(SupportMessage message)
    {
        (db.SupportScope ?? throw new InvalidOperationException("SupportWriteDenied")).BindMessage(message);
        db.SupportMessages.Add(message);
    }
    public Task<SupportAttachment?> FindAttachmentAsync(AuthorizedSupportScope scope, int id, CancellationToken ct) =>
        throw new SupportException("SupportFilesUnavailable");
    public Task ReserveBytesAsync(AuthorizedSupportScope scope, long bytes, long limit, CancellationToken ct) =>
        throw new SupportException("SupportFilesUnavailable");
}
