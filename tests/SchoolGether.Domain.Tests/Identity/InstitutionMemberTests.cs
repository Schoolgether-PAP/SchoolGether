using SchoolGether.Domain.Identity;

namespace SchoolGether.Domain.Tests.Identity;

public sealed class InstitutionMemberTests
{
    [Theory]
    [InlineData(AccountRole.InstitutionAdmin)]
    [InlineData(AccountRole.Teacher)]
    [InlineData(AccountRole.Student)]
    public void Member_RequiresOneInstitutionAndKeepsRole(AccountRole role)
    {
        var member = new InstitutionMember(1, 2, "  Nome  ", role, 3);
        Assert.Equal(2, member.InstitutionId);
        Assert.Equal("Nome", member.Name);
        Assert.Equal(role, member.Role);
    }

    [Theory]
    [InlineData(0, 2, AccountRole.Student)]
    [InlineData(1, 0, AccountRole.Student)]
    [InlineData(1, 2, AccountRole.PlatformAdmin)]
    [InlineData(1, 2, (AccountRole)99)]
    public void Member_RejectsInvalidAssociationOrGlobalRole(int userId, int institutionId, AccountRole role) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new InstitutionMember(userId, institutionId, "Nome", role, 3));
}
