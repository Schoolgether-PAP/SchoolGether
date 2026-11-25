namespace SchoolGether.Application.Academic;

public sealed record LearningContext(int CourseId, string CourseName, int ClassId, string ClassName,
    int UnitId, string UnitName, int StudyYear, string AcademicYear);

// The actor comes exclusively from the authenticated server session. Callers cannot supply a user id.
public interface ILearningContextDirectory
{
    Task<bool> LockForWriteAsync(int courseId,int classId,int unitId,string academicYear,CancellationToken ct);
    Task<IReadOnlyList<LearningContext>> ListAsync(string academicYear, CancellationToken ct);
}
