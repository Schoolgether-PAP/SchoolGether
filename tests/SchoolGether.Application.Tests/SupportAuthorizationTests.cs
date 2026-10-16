using SchoolGether.Application.Audit;
using SchoolGether.Application.Identity;
using SchoolGether.Application.Platform;
using SchoolGether.Application.Support;
using SchoolGether.Domain.Identity;

namespace SchoolGether.Application.Tests;

public sealed class SupportAuthorizationTests
{
    [Theory]
    [InlineData(7, 3, "Teacher", 7, 4, AccountRole.Teacher)]
    [InlineData(7, 3, "Teacher", 8, 3, AccountRole.Teacher)]
    [InlineData(7, 3, "Teacher", 7, 3, AccountRole.InstitutionAdmin)]
    [InlineData(7, null, "Teacher", 7, null, AccountRole.Teacher)]
    public async Task PrincipalMustMatchCurrentAccountAndHaveSchoolInstitution(int id, int? tenant, string role, int actualId, int? actualTenant, AccountRole actualRole)
    {
        var auth = Authorization(new Principal(id, tenant, role), new(actualId, "test@example.test", "Atual", actualRole, actualTenant, "test"));
        var failure = await Assert.ThrowsAsync<SupportException>(() => auth.ResolveAsync(default));
        Assert.Equal("InvalidSession", failure.Code);
    }
    [Fact]
    public async Task NameComesFromCurrentDirectory_NotCaller()
    {
        var auth = Authorization(new Principal(7, 3, "Teacher"), new(7, "test@example.test", "Nome atual", AccountRole.Teacher, 3, "test"));
        var actor = await auth.ResolveAsync(default);
        Assert.Equal("Nome atual", actor.Name); Assert.False(actor.CanReadGlobal); Assert.False(actor.CanManageGlobal);
    }
    [Fact]
    public async Task StudentAndRemovedAccountCannotAcquireSupportScope()
    {
        var auth = Authorization(new Principal(7, 3, "Student"), new(7, "test@example.test", "Aluno", AccountRole.Student, 3, "test"));
        Assert.Equal("Forbidden", (await Assert.ThrowsAsync<SupportException>(() => auth.RequireAsync("read", null, default))).Code);
        auth = Authorization(new Principal(7, 3, "Teacher"), null);
        Assert.Equal("InvalidSession", (await Assert.ThrowsAsync<SupportException>(() => auth.ResolveAsync(default))).Code);
    }
    [Fact]
    public void ManageCannotBeDelegatedWithoutRead()
    {
        Assert.Equal("InvalidPermissions", Assert.Throws<PlatformException>(() => PlatformCapabilities.Normalize(["support.manage"])).Code);
        Assert.Equal(new[] { "support.manage", "support.read" }, PlatformCapabilities.Normalize(["support.read", "support.manage"]));
    }
    private static SupportAuthorization Authorization(Principal principal, Account? account)
    {
        var directory = new Directory();
        return new(principal, new Identities(account), new(principal, directory, new Audit()), directory, new() { Enabled = true });
    }
    private sealed record Principal(int? UserId, int? InstitutionId, string? Role) : ICurrentAccount;
    private sealed class Identities(Account? account) : IIdentityStore
    {
        public Task<Account?> FindForAuthenticationAsync(int userId, CancellationToken cancellationToken) => Task.FromResult(account);
        public Task<Account?> VerifyCredentialsAsync(string email, string password, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Account> CreateAsync(CreateAccount account, int createdBy, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MemberPage> ListMembersAsync(int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class Directory : IPlatformAdminDirectory
    {
        public Task<PlatformAdmin?> FindAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformAdminPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformAdmin> CreateAsync(CreatePlatformAdmin request, int createdBy, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformAdmin> UpdateAsync(int id, UpdatePlatformAdmin request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlatformAdmin> PromoteMasterAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class Audit : IAuditWriter
    {
        public void Append(AuditEvent auditEvent) { }
        public Task AppendIndependentAsync(AuditEvent auditEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
