using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SchoolGether.Application.Audit;
using SchoolGether.Application.Platform;
using SchoolGether.Domain.Support;

namespace SchoolGether.Application.Support;

public sealed class SupportService(SupportAuthorization authorization, ISupportStore store,
    ISupportUnitOfWork unitOfWork, IAuditWriter audit, IPlatformAdminDirectory directory, TimeProvider clock,
    ISupportNotifier notifier)
{
    public async Task<SupportTicketResult> CreateAsync(CreateSupportTicket command, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync("create", null, ct);
        RequireNoFiles(command.Files);
        var fingerprint = Fingerprint(scope.Actor.Id, command.Subject, command.Category, command.Priority, command.Description);
        var replay = false;
        var ticket = await unitOfWork.ExecuteAsync(scope, async token =>
        {
            var existing = await store.FindTicketByClientIdAsync(scope, command.ClientId, token);
            if (existing is not null)
            {
                var initial = await store.FindMessageByClientIdAsync(scope, existing.Id, command.ClientId, token);
                RequireReplay(initial, scope.Actor.Id, fingerprint);
                replay = true;
                return existing;
            }
            var now = clock.GetUtcNow().UtcDateTime;
            var created = SupportTicket.Open(scope.Actor.InstitutionId!.Value, scope.Actor.Id, scope.Actor.Name,
                command.ClientId, command.Subject, Parse<SupportCategory>(command.Category), Parse<SupportPriority>(command.Priority), now);
            var message = SupportMessage.CreateInitial(created.InstitutionId, 0, scope.Actor.Id, scope.Actor.Name,
                command.ClientId, command.Description, 0, fingerprint, now);
            created.AddMessage(message, true, now);
            store.AddTicket(created);
            Audit(scope, created, "create");
            return created;
        }, ct);
        if (!replay) await NotifyAsync(ticket.Id, false);
        return await ProjectAsync(ticket, scope, replay, ct);
    }

    public async Task<SupportMessageResult> SendAsync(int ticketId, SendSupportMessage command, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync("reply", ticketId, ct);
        if (command.Internal && !scope.Actor.CanManageGlobal) throw new SupportException("Forbidden");
        RequireNoFiles(command.Files);
        var fingerprint = Fingerprint(scope.Actor.Id, command.Text, command.Internal);
        var replay = false;
        var message = await unitOfWork.ExecuteAsync(scope, async token =>
        {
            var ticket = await store.FindTicketAsync(scope, ticketId, token) ?? throw new SupportException("SupportNotFound");
            var existing = await store.FindMessageByClientIdAsync(scope, ticketId, command.ClientId, token);
            if (existing is not null)
            {
                RequireReplay(existing, scope.Actor.Id, fingerprint);
                replay = true;
                return existing;
            }
            RequireVersion(ticket, command.RowVersion);
            var now = clock.GetUtcNow().UtcDateTime;
            var created = SupportMessage.Create(ticket.InstitutionId, ticket.Id, scope.Actor.Id, scope.Actor.Name,
                command.ClientId, command.Text, command.Internal, 0, fingerprint, now);
            ticket.AddMessage(created, !scope.Global, now);
            store.AddMessage(created);
            Audit(scope, ticket, command.Internal ? "note" : "reply");
            return created;
        }, ct);
        if (!replay) await NotifyAsync(ticketId, command.Internal);
        return SupportProjection.Message(message, replay);
    }

    public async Task<SupportTicketResult> UpdateAsync(int ticketId, UpdateSupportTicket command, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync("manage", ticketId, ct);
        await authorization.RequireAssigneeAsync(command.AssigneeId, ct);
        var status = Parse<SupportStatus>(command.Status);
        var priority = Parse<SupportPriority>(command.Priority);
        var ticket = await unitOfWork.ExecuteAsync(scope, async token =>
        {
            var ticket = await store.FindTicketAsync(scope, ticketId, token) ?? throw new SupportException("SupportNotFound");
            RequireVersion(ticket, command.RowVersion);
            var now = clock.GetUtcNow().UtcDateTime;
            ticket.ChangeStatus(status, now);
            if (ticket.Priority != priority) ticket.ChangePriority(priority, now);
            if (ticket.AssigneeId != command.AssigneeId) ticket.ChangeAssignment(command.AssigneeId, now);
            Audit(scope, ticket, "update");
            return ticket;
        }, ct);
        await NotifyAsync(ticketId, false);
        return await ProjectAsync(ticket, scope, false, ct);
    }

    public async Task<SupportTicketResult> CloseAsync(int ticketId, CloseSupportTicket command, CancellationToken ct)
    {
        var scope = await authorization.RequireAsync("manage", ticketId, ct);
        var ticket = await unitOfWork.ExecuteAsync(scope, async token =>
        {
            var ticket = await store.FindTicketAsync(scope, ticketId, token) ?? throw new SupportException("SupportNotFound");
            RequireVersion(ticket, command.RowVersion);
            ticket.Close(scope.Actor.Id, command.Summary, clock.GetUtcNow().UtcDateTime);
            Audit(scope, ticket, "close");
            return ticket;
        }, ct);
        await NotifyAsync(ticketId, false);
        return await ProjectAsync(ticket, scope, false, ct);
    }

    internal static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse<T>(value, false, out var result) && Enum.IsDefined(result) && Enum.GetName(result) == value
            ? result : throw new SupportException("InvalidSupportInput");
    private static void RequireNoFiles(IReadOnlyList<IncomingAttachment> files)
    { if (files is null || files.Count != 0) throw new SupportException("SupportFilesUnavailable"); }
    private static void RequireVersion(SupportTicket ticket, long version)
    { if (ticket.RowVersion != version) throw new SupportException("VersionConflict"); }
    private static void RequireReplay(SupportMessage? message, int actorId, string fingerprint)
    {
        if (message is null || message.AuthorId != actorId || message.Fingerprint != fingerprint)
            throw new SupportException("SupportReplayConflict");
    }
    private static string Fingerprint(params object?[] parts) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));
    private void Audit(AuthorizedSupportScope scope, SupportTicket ticket, string action) => audit.Append(new(
        new(scope.Actor.Id, scope.Actor.Name), "support." + action, "support.ticket:" + ticket.ClientId.ToString("D"), "success", string.Empty,
        ticket.InstitutionId, After: new Dictionary<string, object?> { ["rowVersion"] = ticket.RowVersion }));
    private async Task<SupportTicketResult> ProjectAsync(SupportTicket ticket, AuthorizedSupportScope scope, bool replay, CancellationToken ct) =>
        SupportProjection.Ticket(ticket, scope, ticket.AssigneeId is int id ? (await directory.FindAsync(id, ct))?.Name : null, replay);
    private async Task NotifyAsync(int ticketId, bool internalOnly)
    {
        // O aviso é posterior ao commit: uma falha de transporte não desfaz uma escrita aceite.
        try { await notifier.ChangedAsync(ticketId, internalOnly, CancellationToken.None); }
        catch { /* O cliente recupera o estado persistido na próxima leitura. */ }
    }
}
