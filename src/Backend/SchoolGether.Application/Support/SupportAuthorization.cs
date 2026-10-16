using SchoolGether.Application.Identity;
using SchoolGether.Application.Platform;

namespace SchoolGether.Application.Support;

public sealed class SupportAuthorization(ICurrentAccount current, IIdentityStore identities,
    PlatformAuthorization platform, IPlatformAdminDirectory directory, SupportOptions options)
{
    public async Task<SupportActor> ResolveAsync(CancellationToken ct)
    {
        var account = current.UserId is int id ? await identities.FindForAuthenticationAsync(id, ct) : null;
        if (account is null || account.Id != current.UserId || account.InstitutionId != current.InstitutionId || account.Role.ToString() != current.Role)
            throw new SupportException("InvalidSession");
        if (current.Role == "PlatformAdmin")
        {
            var admin = await platform.GetContextAsync(ct);
            return new(account.Id, account.Name, current.Role, null, admin.Permissions.Contains(PlatformCapabilities.SupportRead),
                admin.Permissions.Contains(PlatformCapabilities.SupportRead) && admin.Permissions.Contains(PlatformCapabilities.SupportManage));
        }
        if (current.Role is not ("Teacher" or "InstitutionAdmin")) throw new SupportException("Forbidden");
        if (account.InstitutionId is not > 0) throw new SupportException("InvalidSession");
        return new(account.Id, account.Name, current.Role, account.InstitutionId, false, false);
    }

    public async Task<AuthorizedSupportScope> RequireAsync(string operation, int? ticketId, CancellationToken ct)
    {
        var actor = await ResolveAsync(ct);
        if (!options.Enabled) throw new SupportException("SupportDisabled");
        if (actor.Role == "PlatformAdmin" && (!actor.CanReadGlobal || operation == "create" ||
            (operation is "reply" or "manage" && !actor.CanManageGlobal))) throw new SupportException("Forbidden");
        if (actor.Role != "PlatformAdmin" && operation == "manage") throw new SupportException("Forbidden");
        return new(actor, operation, ticketId);
    }

    public async Task<SupportContext> ContextAsync(CancellationToken ct)
    {
        var actor = await ResolveAsync(ct);
        return new(options.Enabled, options.Enabled && actor.Role is "Teacher" or "InstitutionAdmin",
            options.Enabled && actor.CanReadGlobal, options.Enabled && actor.CanManageGlobal);
    }

    public async Task<IReadOnlyList<SupportAssignee>> AssigneesAsync(CancellationToken ct)
    {
        await RequireAsync("manage", null, ct);
        var result = new List<SupportAssignee>();
        for (var page = 1; ; page++)
        {
            var admins = await directory.ListAsync(page, 100, ct);
            result.AddRange(admins.Items.Where(EligibleAssignee).Select(x => new SupportAssignee(x.Id, x.Name)));
            if (page * 100 >= admins.Total) return result;
        }
    }
    public async Task RequireAssigneeAsync(int? id, CancellationToken ct)
    {
        if (id is null) return;
        var admin = await directory.FindAsync(id.Value, ct);
        if (admin is null || !EligibleAssignee(admin)) throw new SupportException("InvalidSupportAssignee");
    }
    private static bool EligibleAssignee(PlatformAdmin admin) => admin.IsActive && (admin.IsMaster ||
        (admin.Permissions.Contains(PlatformCapabilities.SupportRead) && admin.Permissions.Contains(PlatformCapabilities.SupportManage)));
}
