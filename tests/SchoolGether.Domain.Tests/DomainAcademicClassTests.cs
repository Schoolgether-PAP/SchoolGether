using SchoolGether.Domain.Academic;
namespace SchoolGether.Domain.Tests;
public sealed class DomainAcademicClassTests
{
    private static readonly DateTime Now = new(2026,10,20,12,0,0,DateTimeKind.Utc);
    [Fact]
    public void Initial_selection_order_is_canonical_and_replay_survives_mutations()
    {
        var key = Guid.NewGuid(); var value = AcademicClass.Create(1,2,3,key," 10a "," Turma ",10,"2026/2027",[5,4],Now);
        value.Update(1,"B","Nova",Now); value.Archive(2,Now);
        Assert.True(value.IsReplayOf(AcademicClass.Create(1,2,3,key,"10A","Turma",10,"2026/2027",[4,5],Now)));
        Assert.False(value.IsReplayOf(AcademicClass.Create(1,2,3,key,"10A","Turma",10,"2026/2027",[4],Now)));
        Assert.False(value.IsReplayOf(AcademicClass.Create(1,2,4,key,"10A","Turma",10,"2026/2027",[4,5],Now)));
        Assert.Equal(10,value.StudyYear); Assert.Equal("2026/2027",value.AcademicYear); value.Restore(3,Now); Assert.Equal(4,value.RowVersion);
    }
    [Fact]
    public void Invalid_identity_fields_years_selection_and_utc_are_rejected()
    {
        foreach(var ids in new int[][] { [], [1,1], [0], Enumerable.Range(1,101).ToArray() }) Assert.Throws<CourseRuleException>(()=>AcademicClass.Create(1,2,3,Guid.NewGuid(),"A","Turma",10,"2026/2027",ids,Now));
        Assert.Equal(100,AcademicClass.ValidateUnitIds(Enumerable.Range(1,100).ToArray()).Length);
        foreach(var year in new[]{"2026/2028","1899/1900","2200/2201","2026-2027"}) Assert.Throws<CourseRuleException>(()=>AcademicClass.Create(1,2,3,Guid.NewGuid(),"A","Turma",10,year,[1],Now));
        foreach(var code in new[]{"_A","Á","A B",new string('A',31)}) Assert.Throws<CourseRuleException>(()=>AcademicClass.Create(1,2,3,Guid.NewGuid(),code,"Turma",10,"2026/2027",[1],Now));
        Assert.Throws<CourseRuleException>(()=>AcademicClass.Create(1,2,3,Guid.NewGuid(),"A","Turma",21,"2026/2027",[1],Now));
        Assert.Throws<CourseRuleException>(()=>AcademicClass.Create(1,2,3,Guid.NewGuid(),"A","Turma",10,"2026/2027",[1],DateTime.SpecifyKind(Now,DateTimeKind.Local)));
    }
}
