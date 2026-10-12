namespace SchoolGether.Domain.Institutions;

public sealed class Institution
{
    public const int MaxNameLength = 200;

    private Institution() { }

    public Institution(string name, int? createdBy = null)
    {
        Name = ValidateName(name);
        if (createdBy is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(createdBy));
        }

        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public int? CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public long RowVersion { get; private set; } = 1;

    public void Rename(string name)
    {
        if (DeletedAt.HasValue)
        {
            throw new InvalidOperationException("Não é possível alterar uma instituição eliminada.");
        }

        var validatedName = ValidateName(name);
        if (Name == validatedName)
        {
            return;
        }

        Name = validatedName;
        UpdatedAt = DateTime.UtcNow;
        RowVersion++;
    }

    public void Delete()
    {
        if (DeletedAt.HasValue)
        {
            return;
        }

        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DeletedAt;
        RowVersion++;
    }

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
        {
            throw new ArgumentException($"O nome não pode exceder {MaxNameLength} caracteres.", nameof(name));
        }

        return trimmedName;
    }
}
