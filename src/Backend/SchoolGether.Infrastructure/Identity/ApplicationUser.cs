using Microsoft.AspNetCore.Identity;

namespace SchoolGether.Infrastructure.Identity;

// Diretório global de credenciais. Os dados institucionais estão em InstitutionMember.
public sealed class ApplicationUser : IdentityUser<int>
{
    public string Name { get; set; } = string.Empty;
    public bool IsPlatformAdmin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public long RowVersion { get; set; } = 1;
}
