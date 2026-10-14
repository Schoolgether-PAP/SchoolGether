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
    public static bool IsValidAcademicYear(string? value)
    {
        if (value is null || value.Length != 9 || value[4] != '/' ||
            value.Where((_, index) => index != 4).Any(c => c is < '0' or > '9')) return false;
        var first = int.Parse(value.AsSpan(0, 4), System.Globalization.CultureInfo.InvariantCulture);
        var second = int.Parse(value.AsSpan(5, 4), System.Globalization.CultureInfo.InvariantCulture);
        return first is >= 1900 and <= 2199 && second == first + 1;
    }
    public void Complete(string institutionType, string? locality, string academicYear, DateTime completedAt)
    {
        if (!IsValidType(institutionType)) throw new ArgumentException("InvalidInstitutionType", nameof(institutionType));
        if (locality?.Trim().Length > 100) throw new ArgumentException("InvalidLocality", nameof(locality));
        if (!IsValidAcademicYear(academicYear)) throw new ArgumentException("InvalidAcademicYear", nameof(academicYear));
        InstitutionType = institutionType;
        Locality = string.IsNullOrWhiteSpace(locality) ? null : locality.Trim();
        AcademicYear = academicYear;
        IsComplete = true;
        CompletedAt ??= completedAt;
        UpdatedAt = completedAt;
        RowVersion++;
    }
    public static bool IsValidType(string? value) => value is "School" or "Faculty" or "Polytechnic" or "TrainingCenter" or "Other";
    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public string InstitutionType { get; private set; } = string.Empty;
    public string? Locality { get; private set; }
    public string? AcademicYear { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsComplete { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public long RowVersion { get; private set; } = 1;
}
