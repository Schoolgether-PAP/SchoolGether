using SchoolGether.Domain.Identity;

namespace SchoolGether.Application.Identity;

public sealed record Account(int Id, string Email, string Name, AccountRole Role, int? InstitutionId, string SecurityStamp);
public sealed record CreateAccount(string Email, string Password, string Name, AccountRole Role, int? InstitutionId)
{
    public static CreateAccount FromRoleName(string email, string password, string name, string role, int? institutionId) =>
        new(email, password, name, Enum.Parse<AccountRole>(role), institutionId);
}
public sealed record Session(string Hash, int UserId, Guid FamilyId, string SecurityStamp, DateTime ExpiresAt,
    bool Invalid);
public sealed record AccessToken(string Value, DateTime ExpiresAt);
public sealed record Tokens(AccessToken Access, string Refresh, DateTime RefreshExpiresAt, Account Account);
public sealed record MemberPage(IReadOnlyList<Account> Items, int Total, int Page, int PageSize);

public sealed class IdentityException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
