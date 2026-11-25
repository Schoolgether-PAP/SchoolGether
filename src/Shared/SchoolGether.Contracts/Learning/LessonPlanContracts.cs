namespace SchoolGether.Contracts.Learning;
public sealed record CreateLessonPlanRequest(Guid ClientId, int CourseId, int ClassId, int UnitId, string AcademicYear, string Title, DateOnly Date, string? Objectives, string? Content, string? Resources);
public sealed record UpdateLessonPlanRequest(long RowVersion, string Title, DateOnly Date, string? Objectives, string? Content, string? Resources);
public sealed record LessonPlanStateRequest(long RowVersion);
public sealed record LessonPlanResponse(int Id, Guid ClientId, int CourseId, int ClassId, int UnitId, string AcademicYear, string Title, DateOnly Date, string? Objectives, string? Content, string? Resources, bool IsArchived, long RowVersion);
public sealed record LessonPlanPageResponse(IReadOnlyList<LessonPlanResponse> Items, int Total, int Page, int PageSize);
