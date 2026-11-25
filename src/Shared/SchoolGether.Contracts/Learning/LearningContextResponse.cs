namespace SchoolGether.Contracts.Learning;

public sealed record LearningContextResponse(int CourseId, string CourseName, int ClassId, string ClassName,
    int UnitId, string UnitName, int StudyYear, string AcademicYear);
