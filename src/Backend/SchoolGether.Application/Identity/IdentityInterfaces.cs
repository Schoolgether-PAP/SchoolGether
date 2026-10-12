namespace SchoolGether.Application.Identity;

public interface ICurrentAccount
{
    int? UserId { get; }
    int? InstitutionId { get; }
    string? Role { get; }
}

public interface IIdentityStore
{
    Task<Account?> VerifyCredentialsAsync(string email, string password, CancellationToken cancellationToken);
    // Operações globais apenas para autenticar a própria conta; não são listagens de dados de clientes.
    Task<Account?> FindForAuthenticationAsync(int userId, CancellationToken cancellationToken);
    Task<Account> CreateAsync(CreateAccount account, int createdBy, CancellationToken cancellationToken);
    Task<MemberPage> ListMembersAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public interface ISessionStore
{
    Task AddAsync(Session session, CancellationToken cancellationToken);
    Task<Session?> FindAsync(string hash, CancellationToken cancellationToken);
    Task<bool> RotateAsync(Session previous, Session next, DateTime now, CancellationToken cancellationToken);
    Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken);
}

public interface IAccessTokenIssuer
{
    AccessToken Issue(Account account, DateTime now);
}
