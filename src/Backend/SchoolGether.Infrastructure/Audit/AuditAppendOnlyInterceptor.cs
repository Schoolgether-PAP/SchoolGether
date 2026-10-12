using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SchoolGether.Infrastructure.Audit;

// ExecuteUpdate/ExecuteDelete não passam por SaveChanges; bloquear também estes comandos.
internal sealed class AuditAppendOnlyInterceptor : DbCommandInterceptor
{
    internal static readonly AuditAppendOnlyInterceptor Instance = new();
    private static readonly Regex Mutation = new(@"\b(?:UPDATE|DELETE\s+FROM)\s+[`""\[]?audit_entry\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static void Validate(DbCommand command)
    {
        if (Mutation.IsMatch(command.CommandText)) throw new InvalidOperationException("AuditAppendOnly");
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    { Validate(command); return result; }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Validate(command); return ValueTask.FromResult(result); }
}
