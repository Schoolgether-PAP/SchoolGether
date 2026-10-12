using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Audit;
using SchoolGether.Domain.Audit;
using SchoolGether.Infrastructure.Persistence;

namespace SchoolGether.Infrastructure.Audit;

internal sealed class AuditWriter(SchoolGetherDbContext db, DbContextOptions<SchoolGetherDbContext> options,
    TimeProvider clock) : IAuditWriter
{
    // Lista positiva: um objeto Identity ou um campo novo nunca entra no histórico por acidente.
    private static readonly HashSet<string> AllowedFields = new(StringComparer.Ordinal) {
        "id", "name", "isMaster", "isActive", "permissions", "rowVersion", "isSuspended", "suspensionReason", "role", "institutionId" };

    public void Append(AuditEvent auditEvent) => db.AuditEntries.Add(ToEntry(auditEvent));

    public async Task AppendIndependentAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        await using var independent = new SchoolGetherDbContext(options);
        independent.AuditEntries.Add(ToEntry(auditEvent));
        await independent.SaveChangesAsync(cancellationToken);
    }

    private AuditEntry ToEntry(AuditEvent value) => new(clock.GetUtcNow().UtcDateTime,
        value.Actor.Id, value.Actor.Name, value.InstitutionId, value.Action, value.Target, value.Result, value.Reason,
        Sanitize(value.Before), Sanitize(value.After), value.RequestReference ?? Activity.Current?.TraceId.ToString());

    private static string? Sanitize(IReadOnlyDictionary<string, object?>? values) => values is null ? null :
        JsonSerializer.Serialize(values.Where(x => AllowedFields.Contains(x.Key) && IsSafeValue(x.Value))
            .ToDictionary(x => x.Key, x => x.Value));

    private static bool IsSafeValue(object? value) => value is null or string or bool or int or long or string[];
}
