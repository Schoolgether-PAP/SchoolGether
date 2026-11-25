namespace SchoolGether.Contracts.Learning;
public sealed record CreateLearningMaterialRequest(Guid ClientId, int CourseId, int ClassId, int UnitId, string AcademicYear, string Title, string? Content, string? Url, bool IsPublished);
public sealed record UpdateLearningMaterialRequest(long RowVersion, string Title, string? Content, string? Url, bool IsPublished);
public sealed record LearningMaterialStateRequest(long RowVersion);
public sealed record LearningMaterialResponse(int Id, Guid ClientId, int CourseId, int ClassId, int UnitId, string AcademicYear, string Title, string? Content, string? Url, bool IsPublished, bool IsArchived, long RowVersion);
public sealed record LearningMaterialPageResponse(IReadOnlyList<LearningMaterialResponse> Items, int Total, int Page, int PageSize);
