namespace SchoolGether.Infrastructure.Identity;

public sealed class RefreshSession
{
    public int Id { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public int UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string SecurityStamp { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public long RowVersion { get; set; } = 1;
}
