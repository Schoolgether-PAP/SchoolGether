using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SchoolGether.Domain.Institutions;
namespace SchoolGether.Domain.Academic;

public sealed class AcademicAssignment
{
    private AcademicAssignment() { }
    public int Id { get; private set; }
    public int InstitutionId { get; private set; }
    public int CourseId { get; private set; }
    public int ClassId { get; private set; }
    public int UnitId { get; private set; }
    public int TeacherId { get; private set; }
    public int? ActiveTeacherId { get; private set; }
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
    public static AcademicAssignment Create(int institutionId,int courseId,int classId,int teacherId,int unitId,int studyYear,string academicYear,int actorId,Guid clientId,DateTime now)
    {
        if(institutionId<=0||courseId<=0||classId<=0||teacherId<=0||unitId<=0||actorId<=0||clientId==Guid.Empty||studyYear is <1 or >20||!InstitutionProfile.IsValidAcademicYear(academicYear)||now.Kind!=DateTimeKind.Utc)throw new CourseRuleException("InvalidAssignmentInput");
        return new(){InstitutionId=institutionId,CourseId=courseId,ClassId=classId,TeacherId=teacherId,UnitId=unitId,ActiveTeacherId=teacherId,StudyYear=studyYear,AcademicYear=academicYear,CreatedBy=actorId,ClientId=clientId,CreatedAt=now,
            CreationFingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new object[]{courseId,classId,teacherId,unitId,studyYear,academicYear}))))};
    }
    public bool IsReplayOf(AcademicAssignment initial)=>InstitutionId==initial.InstitutionId&&CourseId==initial.CourseId&&ClassId==initial.ClassId&&TeacherId==initial.TeacherId&&UnitId==initial.UnitId&&ClientId==initial.ClientId&&CreatedBy==initial.CreatedBy&&CreationFingerprint==initial.CreationFingerprint;
    public void Cancel(long version,DateTime now)
    { RequireVersion(version,now);if(!IsActive)throw new CourseRuleException("AssignmentAlreadyCancelled");IsActive=false;ActiveTeacherId=null;DeletedAt=now;Advance(now); }
    public void Restore(long version,DateTime now)
    { RequireVersion(version,now);if(IsActive)throw new CourseRuleException("AssignmentAlreadyActive");IsActive=true;ActiveTeacherId=TeacherId;DeletedAt=null;Advance(now); }
    private void RequireVersion(long version,DateTime now)
    {if(version<1||now.Kind!=DateTimeKind.Utc)throw new CourseRuleException("InvalidAssignmentInput");if(version!=RowVersion)throw new CourseRuleException("VersionConflict");}
    private void Advance(DateTime now){RowVersion=checked(RowVersion+1);UpdatedAt=now;}
}
