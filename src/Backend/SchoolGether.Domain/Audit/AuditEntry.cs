namespace SchoolGether.Domain.Audit;

// Histórico global: sem referências navegáveis a credenciais ou dados institucionais.
public sealed class AuditEntry
{
    private AuditEntry() { }
    public AuditEntry(DateTime createdAt, int actorId, string actorName, int? institutionId,
        string action, string target, string result, string reason, string? before, string? after, string? requestReference)
    {
        CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
        ActorId = actorId; ActorName = actorName; InstitutionId = institutionId;
        Action = action; Target = target; Result = result; Reason = reason;
        Before = before; After = after; RequestReference = requestReference;
    }
    public int Id { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int ActorId { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public int? InstitutionId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Target { get; private set; } = string.Empty;
    public string Result { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string? Before { get; private set; }
    public string? After { get; private set; }
    public string? RequestReference { get; private set; }
}
