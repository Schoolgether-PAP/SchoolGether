namespace SchoolGether.Domain.Institutions;

public sealed class InstitutionProfile
{
    private InstitutionProfile() { }
    public InstitutionProfile(int institutionId, string institutionType, string? locality, int createdBy)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(institutionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(createdBy);
        if (!IsValidType(institutionType)) throw new ArgumentException("InvalidInstitutionType", nameof(institutionType));
        if (locality?.Trim().Length > 100) throw new ArgumentException("InvalidLocality", nameof(locality));
        InstitutionId = institutionId; InstitutionType = institutionType;
        Locality = string.IsNullOrWhiteSpace(locality) ? null : locality.Trim(); CreatedBy = createdBy;
    }
    public static bool IsValidType(string? value) => value is "School" or "Faculty" or "Polytechnic" or "TrainingCenter" or "Other";
    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public string InstitutionType { get; private set; } = string.Empty;
    public string? Locality { get; private set; }
    public string? AcademicYear { get; private set; }
    public bool IsComplete { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public long RowVersion { get; private set; } = 1;
}
