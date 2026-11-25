namespace SchoolGether.Domain.Learning;
public sealed class LearningMaterialException(string code) : Exception(code) { public string Code { get; } = code; }
public sealed class LearningMaterial
{
    public int Id { get; private set; } public Guid ClientId { get; private set; } public int InstitutionId { get; private set; } public int TeacherId { get; private set; }
    public int CourseId { get; private set; } public int ClassId { get; private set; } public int UnitId { get; private set; } public string AcademicYear { get; private set; } = "";
    public string Title { get; private set; } = ""; public string? Content { get; private set; } public string? Url { get; private set; } public bool IsPublished { get; private set; }
    public string CreateHash { get; private set; } = ""; public bool IsArchived { get; private set; } public long RowVersion { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; } public DateTime? UpdatedAt { get; private set; }
    private LearningMaterial() { }
    public static LearningMaterial Create(int tenant, int teacher, Guid key, int course, int group, int unit, string year, string title, string? content, string? url, bool published, string hash, DateTime now)
    {
        if (key == Guid.Empty || course <= 0 || group <= 0 || unit <= 0) throw new LearningMaterialException("InvalidInput");
        var p = new LearningMaterial { InstitutionId = tenant, TeacherId = teacher, ClientId = key, CourseId = course, ClassId = group, UnitId = unit, AcademicYear = year, CreateHash = hash, CreatedAt = now };
        p.Fields(title, content, url, published); return p;
    }
    public void Update(long version, string title, string? content, string? url, bool published, DateTime now) { Check(version); Fields(title, content, url, published); Touch(now); }
    public void Archive(long version, DateTime now) { Check(version); IsArchived = true; Touch(now); }
    void Check(long version) { if (version != RowVersion) throw new LearningMaterialException("VersionConflict"); if (IsArchived) throw new LearningMaterialException("StateConflict"); }
    void Touch(DateTime now) { RowVersion++; UpdatedAt = now; }
    void Fields(string title, string? content, string? url, bool published)
    {
        url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200 || content?.Length > 8000 || url?.Length > 2048 || (url is not null && (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(parsed.UserInfo)))) throw new LearningMaterialException("InvalidInput");
        Title = title.Trim(); Content = content?.Trim(); Url = url; IsPublished = published;
    }
}
