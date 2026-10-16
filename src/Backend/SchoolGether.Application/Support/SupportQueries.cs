using SchoolGether.Application.Platform;
using SchoolGether.Domain.Support;

namespace SchoolGether.Application.Support;

public sealed class SupportQueries(SupportAuthorization authorization, ISupportStore store, IPlatformAdminDirectory directory, IFileStorage storage)
{
    public async Task<SupportTicketPage> ListAsync(SupportTicketQuery query, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync("read", null, ct);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue ||
            query.Search?.Length > 150 || query.InstitutionId is <= 0 || query.AssigneeId is <= 0)
            throw new SupportException("InvalidSupportInput");
        if (!scope.Global && query.InstitutionId is not null && query.InstitutionId != scope.Actor.InstitutionId)
            throw new SupportException("Forbidden");
        if (query.Status is not null) SupportService.Parse<SupportStatus>(query.Status);
        if (query.Priority is not null) SupportService.Parse<SupportPriority>(query.Priority);
        var page = await store.ListAsync(scope, query, ct);
        var items = new List<SupportTicketResult>();
        foreach (var item in page.Items)
            items.Add(item with { AssigneeName = item.AssigneeId is int id ? (await directory.FindAsync(id, ct))?.Name : null });
        return page with { Items = items };
    }
    public async Task<SupportTicketResult> GetAsync(int ticketId, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync("read", ticketId, ct);
        var ticket = await store.FindTicketAsync(scope, ticketId, ct) ?? throw new SupportException("SupportNotFound");
        return SupportProjection.Ticket(ticket, scope, ticket.AssigneeId is int id ? (await directory.FindAsync(id, ct))?.Name : null);
    }
    public async Task<SupportMessagePage> MessagesAsync(int ticketId, int? beforeId, int? afterId, int take, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync("read", ticketId, ct);
        if (take is < 1 or > 50 || (beforeId.HasValue && afterId.HasValue) || beforeId is <= 0 || afterId is <= 0)
            throw new SupportException("InvalidSupportInput");
        _ = await store.FindTicketAsync(scope, ticketId, ct) ?? throw new SupportException("SupportNotFound");
        return await store.ListMessagesAsync(scope, ticketId, beforeId, afterId, take, ct);
    }
    public async Task<SupportDownload> DownloadAsync(int attachmentId, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync("read", null, ct);
        var attachment = await store.FindAttachmentAsync(scope, attachmentId, ct) ?? throw new SupportException("SupportNotFound");
        return new(await storage.OpenReadAsync(attachment.StorageKey, ct), attachment.FileName, attachment.ContentType);
    }
}
