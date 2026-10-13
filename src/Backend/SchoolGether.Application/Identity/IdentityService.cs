using System.Security.Cryptography;
using System.Text;
using SchoolGether.Application.Institutions;
using SchoolGether.Domain.Identity;
using SchoolGether.Application.Audit;
using SchoolGether.Application.Platform;

namespace SchoolGether.Application.Identity;

public sealed class IdentityService(IIdentityStore accounts, ISessionStore sessions, IAccessTokenIssuer issuer,
    ICurrentAccount current, IInstitutionDirectory institutions, TimeProvider clock,
    PlatformAuthorization platform, IAdministrativeUnitOfWork unitOfWork, IAuditWriter audit)
{
    public async Task<Tokens> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var account = await accounts.VerifyCredentialsAsync(email.Trim(), password, cancellationToken)
            ?? throw new IdentityException("InvalidCredentials");
        var now = clock.GetUtcNow().UtcDateTime;
        var refresh = NewRefreshToken();
        var session = new Session(Hash(refresh), account.Id, Guid.NewGuid(), account.SecurityStamp, now.AddDays(14), false);
        var access = issuer.Issue(account, now);
        await sessions.AddAsync(session, cancellationToken);
        return new Tokens(access, refresh, session.ExpiresAt, account);
    }

    public async Task<Tokens> RefreshAsync(string token, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var previous = await sessions.FindAsync(Hash(token), cancellationToken)
            ?? throw new IdentityException("InvalidSession");
        if (previous.Invalid)
        {
            await sessions.RevokeFamilyAsync(previous.FamilyId, now, cancellationToken);
            throw new IdentityException("InvalidSession");
        }
        var account = await accounts.FindForAuthenticationAsync(previous.UserId, cancellationToken);
        if (previous.ExpiresAt <= now || account is null || account.SecurityStamp != previous.SecurityStamp)
        {
            await sessions.RevokeFamilyAsync(previous.FamilyId, now, cancellationToken);
            throw new IdentityException("InvalidSession");
        }
        var refresh = NewRefreshToken();
        var next = previous with { Hash = Hash(refresh) };
        var access = issuer.Issue(account, now);
        if (!await sessions.RotateAsync(previous, next, now, cancellationToken))
            throw new IdentityException("InvalidSession");
        return new Tokens(access, refresh, next.ExpiresAt, account);
    }

    public async Task LogoutAsync(string token, CancellationToken cancellationToken)
    {
        var session = await sessions.FindAsync(Hash(token), cancellationToken);
        if (session is not null)
            await sessions.RevokeFamilyAsync(session.FamilyId, clock.GetUtcNow().UtcDateTime, cancellationToken);
    }

    public async Task<Account> GetMeAsync(CancellationToken cancellationToken)
    {
        var account = current.UserId is int userId
            ? await accounts.FindForAuthenticationAsync(userId, cancellationToken) : null;
        if (account is null || account.InstitutionId != current.InstitutionId || account.Role.ToString() != current.Role)
            throw new IdentityException("InvalidSession");
        return account;
    }

    public async Task<Account> CreateAccountAsync(CreateAccount request, CancellationToken cancellationToken)
    {
        var actor = await GetMeAsync(cancellationToken);
        if (request.Role == AccountRole.PlatformAdmin || !Enum.IsDefined(request.Role))
            throw new IdentityException("Forbidden");
        var institutionId = request.InstitutionId;
        if (current.Role == nameof(AccountRole.InstitutionAdmin))
        {
            if (request.Role == AccountRole.InstitutionAdmin ||
                (institutionId.HasValue && institutionId != current.InstitutionId))
                throw new IdentityException("Forbidden");
            institutionId = current.InstitutionId;
        }
        else if (current.Role != nameof(AccountRole.PlatformAdmin))
            throw new IdentityException("Forbidden");
        else await platform.RequireAsync(PlatformCapabilities.InstitutionsWrite, cancellationToken);
        if (institutionId is not > 0 || !await institutions.ExistsAsync(institutionId.Value, cancellationToken))
            throw new IdentityException("InvalidInstitution");
        if (actor.Role != AccountRole.PlatformAdmin)
            return await accounts.CreateAsync(request with { InstitutionId = institutionId }, actor.Id, cancellationToken);
        return await unitOfWork.ExecuteAsync(async token =>
        {
            var created = await accounts.CreateAsync(request with { InstitutionId = institutionId }, actor.Id, token);
            audit.Append(new AuditEvent(new AuditActor(actor.Id, actor.Name), "institution.account.create", created.Id.ToString(),
                "success", "Criação pela API de contas", institutionId, After: new Dictionary<string, object?> {
                    ["id"] = created.Id, ["name"] = created.Name, ["role"] = created.Role.ToString(), ["institutionId"] = institutionId }));
            return created;
        }, cancellationToken);
    }

    public async Task<MemberPage> ListMembersAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        await GetMeAsync(cancellationToken);
        if (current.Role != nameof(AccountRole.InstitutionAdmin) || current.InstitutionId is null)
            throw new IdentityException("Forbidden");
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100)
            throw new IdentityException("InvalidPagination");
        return await accounts.ListMembersAsync(page, pageSize, cancellationToken);
    }

    private static string NewRefreshToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
