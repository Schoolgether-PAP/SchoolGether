using SchoolGether.Application.Identity;

namespace SchoolGether.Application.Academic;

public sealed class AuthorizedAcademicScope
{
    internal AuthorizedAcademicScope(int actorId, string actorName, int institutionId, string operation, int? courseId)
    { ActorId = actorId; ActorName = actorName; InstitutionId = institutionId; Operation = operation; CourseId = courseId; }
    public int ActorId { get; }
    public string ActorName { get; }
    public int InstitutionId { get; }
    public string Operation { get; }
    public int? CourseId { get; }
}
public sealed class AcademicAuthorization(ICurrentAccount current, IIdentityStore identities)
{
    public async Task<AuthorizedCourseUnitScope> RequireUnitAsync(string operation, int courseId, int? unitId = null, CancellationToken ct = default)
    {
        var scope = await RequireAsync(operation, courseId, ct);
        return new(scope.ActorId, scope.ActorName, scope.InstitutionId, operation, courseId, unitId);
    }
    public async Task<AuthorizedAcademicScope> RequireAsync(string operation, int? courseId = null, CancellationToken ct = default)
    {
        var account = current.UserId is int id ? await identities.FindForAuthenticationAsync(id, ct) : null;
        if (account is null || account.Id != current.UserId || account.InstitutionId != current.InstitutionId || account.Role.ToString() != current.Role)
            throw new AcademicException("InvalidSession");
        if (current.Role != "InstitutionAdmin") throw new AcademicException("Forbidden");
        if (account.InstitutionId is not > 0) throw new AcademicException("InvalidSession");
        if (operation is not ("read" or "create" or "update" or "archive" or "restore")) throw new AcademicException("Forbidden");
        return new(account.Id, account.Name, account.InstitutionId.Value, operation, courseId);
    }
}
