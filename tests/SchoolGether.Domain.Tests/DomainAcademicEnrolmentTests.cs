using SchoolGether.Domain.Academic;
namespace SchoolGether.Domain.Tests;
public sealed class DomainAcademicEnrolmentTests
{
    [Fact]
    public void Enrolment_lifecycle_reserves_releases_and_restores_slot_with_immutable_replay()
    {
        var now=DateTime.UtcNow;var key=Guid.NewGuid();
        AcademicEnrolment Create(int student=4,int actor=5) => AcademicEnrolment.Create(1,2,3,student,10,"2026/2027",actor,key,now);
        var value=Create();var original=Create();Assert.True(value.IsReplayOf(original));Assert.False(value.IsReplayOf(Create(6)));Assert.False(value.IsReplayOf(Create(actor:6)));
        Assert.Equal(4,value.ActiveStudentId);Assert.Equal(1,value.RowVersion);
        value.Cancel(1L,now);Assert.False(value.IsActive);Assert.Null(value.ActiveStudentId);Assert.True(value.IsReplayOf(original));
        var stale=Assert.Throws<CourseRuleException>(()=>value.Restore(1L,now));Assert.Equal("VersionConflict",stale.Code);
        value.Restore(2L,now);Assert.True(value.IsActive);Assert.Equal(4,value.ActiveStudentId);Assert.Equal(3,value.RowVersion);
        Assert.Equal("EnrolmentAlreadyActive",Assert.Throws<CourseRuleException>(()=>value.Restore(3L,now)).Code);
        Assert.Equal("InvalidEnrolmentInput",Assert.Throws<CourseRuleException>(()=>value.Cancel(3L,DateTime.Now)).Code);
    }
}
