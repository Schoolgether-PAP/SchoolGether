using SchoolGether.Application.Identity;

namespace SchoolGether.Api.Identity;

public sealed class CurrentAccount(IHttpContextAccessor accessor) : ICurrentAccount
{
    public int? UserId => ReadId("sub");
    public int? InstitutionId => ReadId("institution_id");
    public string? Role => accessor.HttpContext?.User.FindFirst("role")?.Value;
    private int? ReadId(string type) => int.TryParse(accessor.HttpContext?.User.FindFirst(type)?.Value, out var id) && id > 0 ? id : null;
}
