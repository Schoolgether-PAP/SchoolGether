using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SchoolGether.Domain.Institutions;
namespace SchoolGether.Domain.Academic;

public sealed class AcademicEnrolment
{
    private AcademicEnrolment() { }
    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public int CourseId { get; private set; }
    public int ClassId { get; private set; }
    public int StudentId { get; private set; }
    public int? ActiveStudentId { get; private set; }
    public int StudyYear { get; private set; }
    public string AcademicYear { get; private set; } = string.Empty;
    public Guid ClientId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public long RowVersion { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public int CreatedBy { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string CreationFingerprint { get; private set; } = string.Empty;
    public static AcademicEnrolment Create(int institutionId,int courseId,int classId,int studentId,int studyYear,string academicYear,int actorId,Guid clientId,DateTime now)
    {
        if(institutionId<=0||courseId<=0||classId<=0||studentId<=0||actorId<=0||clientId==Guid.Empty||studyYear is <1 or >20||!InstitutionProfile.IsValidAcademicYear(academicYear)||now.Kind!=DateTimeKind.Utc)throw new CourseRuleException("InvalidEnrolmentInput");
        return new(){InstitutionId=institutionId,CourseId=courseId,ClassId=classId,StudentId=studentId,ActiveStudentId=studentId,StudyYear=studyYear,AcademicYear=academicYear,CreatedBy=actorId,ClientId=clientId,CreatedAt=now,
            CreationFingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new object[]{courseId,classId,studentId,studyYear,academicYear}))))};
    }
    public bool IsReplayOf(AcademicEnrolment initial)=>InstitutionId==initial.InstitutionId&&CourseId==initial.CourseId&&ClassId==initial.ClassId&&StudentId==initial.StudentId&&ClientId==initial.ClientId&&CreatedBy==initial.CreatedBy&&CreationFingerprint==initial.CreationFingerprint;
    public void Cancel(long version,DateTime now)
    { RequireVersion(version,now);if(!IsActive)throw new CourseRuleException("EnrolmentAlreadyCancelled");IsActive=false;ActiveStudentId=null;DeletedAt=now;Advance(now); }
    public void Restore(long version,DateTime now)
    { RequireVersion(version,now);if(IsActive)throw new CourseRuleException("EnrolmentAlreadyActive");IsActive=true;ActiveStudentId=StudentId;DeletedAt=null;Advance(now); }
    private void RequireVersion(long version,DateTime now)
    {if(version<1||now.Kind!=DateTimeKind.Utc)throw new CourseRuleException("InvalidEnrolmentInput");if(version!=RowVersion)throw new CourseRuleException("VersionConflict");}
    private void Advance(DateTime now){RowVersion=checked(RowVersion+1);UpdatedAt=now;}
}
