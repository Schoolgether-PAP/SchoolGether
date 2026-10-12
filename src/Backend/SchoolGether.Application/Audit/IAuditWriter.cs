namespace SchoolGether.Application.Audit;

public sealed record AuditActor(int Id, string Name);
public sealed record AuditEvent(AuditActor Actor, string Action, string Target, string Result, string Reason,
    int? InstitutionId = null, IReadOnlyDictionary<string, object?>? Before = null,
    IReadOnlyDictionary<string, object?>? After = null, string? RequestReference = null);
public sealed record AuditRecord(int Id, DateTime CreatedAt, int ActorId, string ActorName, int? InstitutionId,
    string Action, string Target, string Result, string Reason, string? Before, string? After);
public sealed record AuditPage(IReadOnlyList<AuditRecord> Items, int Total, int Page, int PageSize);

public interface IAuditWriter
{
    // Apenas prepara o evento; SaveChanges/transação pertencem ao chamador.
    void Append(AuditEvent auditEvent);
    // Contexto separado: nunca grava alterações de negócio pendentes num pedido recusado.
    Task AppendIndependentAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}

public interface IAuditReader
{
    Task<AuditPage> ListAsync(int page, int pageSize, string? search, CancellationToken cancellationToken);
}

public interface IAdministrativeUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
