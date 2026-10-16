using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using SchoolGether.Application.Support;

namespace SchoolGether.Api.Support;

[Authorize]
public sealed class SupportHub(SupportAuthorization authorization, ISupportStore store,
    SupportConnectionRegistry connections, TimeProvider clock, IStringLocalizer<ApiMessages> messages) : Hub
{
    public override async Task OnConnectedAsync()
    {
        try
        {
            var (session, expiresAt) = SupportConnectionRegistry.ReadSession(Context.User);
            if (expiresAt <= clock.GetUtcNow()) throw new SupportException("InvalidSession");
            var scope = await authorization.RequireSessionAsync(session, null, Context.ConnectionAborted);
            connections.Add(new(Context.ConnectionId, session, expiresAt, scope.Global, Context.Abort));
            await base.OnConnectedAsync();
        }
        catch (SupportException) { Context.Abort(); }
    }

    public Task JoinTicket(int ticketId) => InvokeAsync(ticketId, join: true);
    public Task LeaveTicket(int ticketId) => InvokeAsync(ticketId, join: false);

    private async Task InvokeAsync(int ticketId, bool join)
    {
        var connection = connections.Find(Context.ConnectionId);
        if (connection is null) throw new HubException(messages["InvalidSession"]);
        await connection.Gate.WaitAsync(Context.ConnectionAborted);
        try
        {
            if (connection.ExpiresAt <= clock.GetUtcNow()) throw new SupportException("InvalidSession");
            var scope = await authorization.RequireSessionAsync(connection.Session, ticketId, Context.ConnectionAborted);
            if (ticketId <= 0 || await store.FindTicketAsync(scope, ticketId, Context.ConnectionAborted) is null)
                throw new SupportException("SupportNotFound");
            if (join)
            {
                connection.PublicTickets.Add(ticketId);
                if (scope.IncludeInternal) connection.InternalTickets.Add(ticketId);
            }
            else { connection.PublicTickets.Remove(ticketId); connection.InternalTickets.Remove(ticketId); }
        }
        catch (SupportException exception)
        {
            if (exception.Code is "InvalidSession" or "Forbidden" or "SupportDisabled")
            { connections.Remove(connection.Id); Context.Abort(); }
            throw new HubException(messages[exception.Code]);
        }
        finally { connection.Gate.Release(); }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    { connections.Remove(Context.ConnectionId); return base.OnDisconnectedAsync(exception); }
}
