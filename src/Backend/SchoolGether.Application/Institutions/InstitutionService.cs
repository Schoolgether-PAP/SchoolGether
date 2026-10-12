using SchoolGether.Application.Identity;
using SchoolGether.Application.Audit;
using SchoolGether.Application.Platform;

namespace SchoolGether.Application.Institutions;

public sealed class InstitutionService(IdentityService identity, ICurrentAccount current, IInstitutionDirectory institutions,
    PlatformAuthorization platform, IAdministrativeUnitOfWork unitOfWork, IAuditWriter audit)
{
    public async Task<int> CreateAsync(string name, CancellationToken cancellationToken)
    {
        await identity.GetMeAsync(cancellationToken);
        var actor = await platform.RequireAsync(PlatformCapabilities.InstitutionsWrite, cancellationToken);
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var id = await institutions.CreateAsync(name, current.UserId!.Value, token);
            audit.Append(new AuditEvent(new AuditActor(actor.Id, actor.Name), "institution.create", id.ToString(),
                "success", "Criação pela API de instituições", id, After: new Dictionary<string, object?> { ["id"] = id, ["name"] = name.Trim() }));
            return id;
        }, cancellationToken);
    }
}
