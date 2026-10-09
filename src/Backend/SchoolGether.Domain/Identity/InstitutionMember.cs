namespace SchoolGether.Domain.Identity;

public sealed class InstitutionMember
{
    private InstitutionMember() { }

    public InstitutionMember(int userId, int institutionId, string name, AccountRole role, int createdBy)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(institutionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(createdBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Trim().Length > 150) throw new ArgumentException("NameTooLong", nameof(name));
        if (role is not (AccountRole.InstitutionAdmin or AccountRole.Teacher or AccountRole.Student))
            throw new ArgumentOutOfRangeException(nameof(role));
        UserId = userId;
        InstitutionId = institutionId;
        Name = name.Trim();
        Role = role;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
    }

    public int Id { get; private set; }
    public int UserId { get; private set; }
    public int InstitutionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AccountRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public long RowVersion { get; private set; } = 1;
}
