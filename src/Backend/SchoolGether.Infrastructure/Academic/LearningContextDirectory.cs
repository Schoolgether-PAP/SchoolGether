using Microsoft.EntityFrameworkCore;
using SchoolGether.Application.Academic;
using SchoolGether.Application.Identity;
using SchoolGether.Domain.Institutions;
using SchoolGether.Infrastructure.Persistence;
namespace SchoolGether.Infrastructure.Academic;

internal sealed class LearningContextDirectory(SchoolGetherDbContext db, ICurrentAccount current,
    IIdentityStore identities,TimeProvider clock) : ILearningContextDirectory
{
    public async Task<bool> LockForWriteAsync(int courseId,int classId,int unitId,string academicYear,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("LearningTransactionRequired");
        if(current.Role!="Teacher"||current.InstitutionId is not int tenant||current.UserId is not int actor)return false;
        if(db.Database.ProviderName=="Microsoft.EntityFrameworkCore.Sqlite")
            return (await ListAsync(academicYear,ct)).Any(x=>x.CourseId==courseId&&x.ClassId==classId&&x.UnitId==unitId);
        // Current locking read, supported by MySQL and TiDB. Revocation, archive and
        // identity changes must wait until the pedagogical write commits or rolls back.
        var now=clock.GetUtcNow().UtcDateTime;
        var locked=await db.Database.SqlQuery<int>($"""
            SELECT a.id AS Value FROM academic_assignment a
            INNER JOIN academic_class c ON c.id=a.class_id AND c.institution_id=a.institution_id AND c.course_id=a.course_id
            INNER JOIN academic_class_unit l ON l.class_id=c.id AND l.unit_id=a.unit_id AND l.institution_id=a.institution_id
            INNER JOIN academic_course_unit u ON u.id=a.unit_id AND u.institution_id=a.institution_id AND u.course_id=a.course_id
            INNER JOIN academic_course p ON p.id=a.course_id AND p.institution_id=a.institution_id
            INNER JOIN user_account t ON t.id=a.teacher_id
            INNER JOIN institution_member m ON m.user_id=t.id AND m.institution_id=a.institution_id
            INNER JOIN institution i ON i.id=a.institution_id
            WHERE a.institution_id={tenant} AND a.teacher_id={actor} AND a.course_id={courseId}
              AND a.class_id={classId} AND a.unit_id={unitId} AND a.academic_year={academicYear}
              AND c.academic_year=a.academic_year AND a.study_year=c.study_year AND u.study_year=c.study_year
              AND a.is_active=1 AND a.deleted_at IS NULL AND c.is_active=1 AND c.deleted_at IS NULL
              AND l.is_active=1 AND l.deleted_at IS NULL AND u.is_active=1 AND u.deleted_at IS NULL
              AND p.is_active=1 AND p.deleted_at IS NULL AND t.is_active=1 AND t.deleted_at IS NULL
              AND t.is_platform_admin=0 AND (t.lockout_enabled=0 OR t.lockout_end IS NULL OR t.lockout_end<={now})
              AND m.role='Teacher' AND m.deleted_at IS NULL AND i.deleted_at IS NULL AND i.is_suspended=0
            FOR UPDATE
            """).ToArrayAsync(ct);
        return locked.Length==1;
    }
    public async Task<IReadOnlyList<LearningContext>> ListAsync(string academicYear, CancellationToken ct)
    {
        var account = current.UserId is int id ? await identities.FindForAuthenticationAsync(id, ct) : null;
        if (account is null || account.Id != current.UserId || account.InstitutionId != current.InstitutionId || account.Role.ToString() != current.Role)
            throw new AcademicException("InvalidSession");
        if (current.Role is not ("Teacher" or "Student") || account.InstitutionId is not int tenant)
            throw new AcademicException("Forbidden");
        if (!InstitutionProfile.IsValidAcademicYear(academicYear)) throw new AcademicException("InvalidClassInput");
        // Only this internal, authenticated projection bypasses the administrator catalog filters.
        // Every source is bounded by tenant, active state and the requested academic year.
        var links = from link in db.ClassUnits.IgnoreQueryFilters().AsNoTracking()
                    join classroom in db.AcademicClasses.IgnoreQueryFilters().AsNoTracking() on link.ClassId equals classroom.Id
                    join unit in db.CourseUnits.IgnoreQueryFilters().AsNoTracking() on link.UnitId equals unit.Id
                    join course in db.Courses.IgnoreQueryFilters().AsNoTracking() on classroom.CourseId equals course.Id
                    where link.InstitutionId == tenant && classroom.InstitutionId == tenant && unit.InstitutionId == tenant && course.InstitutionId == tenant
                        && link.CourseId == course.Id && unit.CourseId == course.Id && classroom.CourseId == course.Id
                        && link.IsActive && classroom.IsActive && unit.IsActive && course.IsActive
                        && link.DeletedAt == null && classroom.DeletedAt == null && unit.DeletedAt == null && course.DeletedAt == null
                        && unit.StudyYear == classroom.StudyYear && classroom.AcademicYear == academicYear
                    select new { Group = classroom, Unit = unit, Course = course };
        if (current.Role == "Teacher")
            links = links.Where(x => db.Assignments.IgnoreQueryFilters().Any(a => a.InstitutionId == tenant && a.TeacherId == account.Id
                && a.IsActive && a.DeletedAt == null && a.CourseId == x.Course.Id && a.ClassId == x.Group.Id && a.UnitId == x.Unit.Id
                && a.StudyYear == x.Group.StudyYear && a.AcademicYear == academicYear));
        else
            links = links.Where(x => db.Enrolments.IgnoreQueryFilters().Any(e => e.InstitutionId == tenant && e.StudentId == account.Id
                && e.IsActive && e.DeletedAt == null && e.CourseId == x.Course.Id && e.ClassId == x.Group.Id
                && e.StudyYear == x.Group.StudyYear && e.AcademicYear == academicYear));
        return await links.OrderBy(x => x.Course.Name).ThenBy(x => x.Group.Name).ThenBy(x => x.Unit.Name)
            .Select(x => new LearningContext(x.Course.Id, x.Course.Name, x.Group.Id, x.Group.Name, x.Unit.Id, x.Unit.Name, x.Group.StudyYear, x.Group.AcademicYear)).ToArrayAsync(ct);
    }
}
