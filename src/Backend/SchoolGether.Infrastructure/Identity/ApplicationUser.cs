using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using SchoolGether.Application.Platform;

namespace SchoolGether.Infrastructure.Identity;

// Diretório global de credenciais. Os dados institucionais estão em InstitutionMember.
public sealed class ApplicationUser : IdentityUser<int>
{
    public string Name { get; set; } = string.Empty;
    public bool IsPlatformAdmin { get; set; }
    public int? MasterSlot { get; private set; }
    public bool IsMaster => MasterSlot == 1;
    public bool IsActive { get; private set; } = true;
    public string PermissionsJson { get; private set; } = "[]";
    public string[] GetPermissions() => JsonSerializer.Deserialize<string[]>(PermissionsJson) ?? [];

    public void SetPlatformAccess(string name, bool isActive, IEnumerable<string> permissions)
    {
        if (!IsPlatformAdmin || IsMaster) throw new PlatformException("MasterProtected");
        Name = name.Trim(); IsActive = isActive;
        PermissionsJson = JsonSerializer.Serialize(PlatformCapabilities.Normalize(permissions));
    }

    public void PromoteMaster()
    {
        if (!IsPlatformAdmin || !IsActive || DeletedAt.HasValue) throw new PlatformException("InvalidMasterAccount");
        MasterSlot = 1;
        RowVersion++;
        UpdatedAt = DateTime.UtcNow;
    }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public long RowVersion { get; set; } = 1;
}
