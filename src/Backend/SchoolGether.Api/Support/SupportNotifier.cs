using Microsoft.AspNetCore.SignalR;
using SchoolGether.Application.Support;
using SchoolGether.Contracts.Support;

namespace SchoolGether.Api.Support;

public sealed class SupportNotifier(SupportConnectionRegistry connections, IServiceScopeFactory scopes,
    IHubContext<SupportHub> hub, TimeProvider clock, ILogger<SupportNotifier> logger) : ISupportNotifier
{
    public async Task ChangedAsync(int ticketId, bool internalOnly, CancellationToken ct)
    {
        foreach (var connection in connections.Snapshot())
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var token = timeout.Token;
            await connection.Gate.WaitAsync(ct);
            try
            {
                if (connections.Find(connection.Id) != connection) continue;
                if (connection.ExpiresAt <= clock.GetUtcNow()) { Remove(connection); continue; }
                if (!connection.GlobalQueue && !(internalOnly ? connection.InternalTickets : connection.PublicTickets).Contains(ticketId)) continue;
                using var scope = scopes.CreateScope();
                var authorized = await scope.ServiceProvider.GetRequiredService<SupportAuthorization>()
                    .RequireSessionAsync(connection.Session, ticketId, token);
                if (internalOnly && !authorized.IncludeInternal) continue;
                var ticket = await scope.ServiceProvider.GetRequiredService<ISupportStore>().FindTicketAsync(authorized, ticketId, token);
                if (ticket is null) { connection.PublicTickets.Remove(ticketId); connection.InternalTickets.Remove(ticketId); continue; }
                if (connection.ExpiresAt <= clock.GetUtcNow()) { Remove(connection); continue; }
                await hub.Clients.Client(connection.Id).SendAsync("SupportChanged", new SupportChangedEvent(ticket.Id, ticket.RowVersion), token);
            }
            catch (SupportException) { Remove(connection); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                // Sem conteúdo, identidade, token, query ou exceção nos registos.
                logger.LogWarning("Não foi possível entregar um aviso de suporte; o cliente pode recuperar por HTTP.");
            }
            finally { connection.Gate.Release(); }
        }
    }

    private void Remove(SupportConnection connection)
    { connections.Remove(connection.Id); connection.Abort(); }
}
