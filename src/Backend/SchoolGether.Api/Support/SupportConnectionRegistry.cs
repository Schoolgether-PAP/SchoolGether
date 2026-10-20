using System.Collections.Concurrent;
using System.Security.Claims;
using SchoolGether.Application.Support;

namespace SchoolGether.Api.Support;

// A inscrição escolhe candidatos; nunca substitui a autorização atual antes da entrega.
public sealed class SupportConnectionRegistry
{
    private readonly ConcurrentDictionary<string, SupportConnection> connections = new();
    internal void Add(SupportConnection connection) => connections[connection.Id] = connection;
    internal SupportConnection? Find(string id) => connections.GetValueOrDefault(id);
    internal SupportConnection[] Snapshot() => connections.Values.ToArray();
    internal void Remove(string id) => connections.TryRemove(id, out _);

    internal static (SupportSession Session, DateTimeOffset ExpiresAt) ReadSession(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirst("sub")?.Value, out var id) || id <= 0 ||
            !long.TryParse(principal.FindFirst("exp")?.Value, out var expires) ||
            expires < DateTimeOffset.MinValue.ToUnixTimeSeconds() || expires > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            throw new SupportException("InvalidSession");
        int? institutionId = null;
        var institution = principal.FindFirst("institution_id")?.Value;
        if (institution is not null)
        {
            if (!int.TryParse(institution, out var parsed) || parsed <= 0) throw new SupportException("InvalidSession");
            institutionId = parsed;
        }
        return (new(id, institutionId, principal.FindFirst("role")?.Value ?? string.Empty), DateTimeOffset.FromUnixTimeSeconds(expires));
    }
}

internal sealed class SupportConnection(string id, SupportSession session, DateTimeOffset expiresAt, bool globalQueue, Action abort)
{
    internal string Id { get; } = id;
    internal SupportSession Session { get; } = session;
    internal DateTimeOffset ExpiresAt { get; } = expiresAt;
    internal bool GlobalQueue { get; } = globalQueue;
    internal Action Abort { get; } = abort;
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal HashSet<int> PublicTickets { get; } = [];
    internal HashSet<int> InternalTickets { get; } = [];
}
