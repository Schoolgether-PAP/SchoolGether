using SchoolGether.Domain.Academic;
namespace SchoolGether.Domain.Tests;
public sealed class CourseUnitTests
{
    private static readonly DateTime Now = new(2026,10,20,12,0,0,DateTimeKind.Utc);
    [Fact]
    public void Initial_replay_is_bound_to_parent_author_and_programme_after_mutations()
    {
        var key = Guid.NewGuid(); var unit = CourseUnit.Create(1,2,3,key," a_1 "," Nome "," Inicial ",Now);
        Assert.Equal("A_1",unit.Code); Assert.Equal("Inicial",unit.Syllabus);
        unit.Update(1,"B","Alterada","Novo",Now); unit.Archive(2,Now);
        Assert.True(unit.IsReplayOf(CourseUnit.Create(1,2,3,key,"A_1","Nome","Inicial",Now)));
        Assert.False(unit.IsReplayOf(CourseUnit.Create(1,4,3,key,"A_1","Nome","Inicial",Now)));
        Assert.False(unit.IsReplayOf(CourseUnit.Create(1,2,4,key,"A_1","Nome","Inicial",Now)));
        Assert.False(unit.IsReplayOf(CourseUnit.Create(1,2,3,key,"A_1","Nome","Novo",Now)));
        Assert.Equal("CourseUnitArchived",Assert.Throws<CourseRuleException>(()=>unit.Update(3,"B","Vedada",null,Now)).Code);
        unit.Restore(3,Now); Assert.True(unit.IsActive); Assert.Null(unit.DeletedAt); Assert.Equal(4,unit.RowVersion);
        Assert.Equal("VersionConflict",Assert.Throws<CourseRuleException>(()=>unit.Archive(3,Now)).Code);
    }
    [Fact]
    public void Fields_and_utc_validate_before_mutating_and_parent_touch_only_changes_metadata()
    {
        var unit = CourseUnit.Create(1,2,3,Guid.NewGuid(),new string('a',30),new string('N',200)," " + new string('S',5000) + " ",Now);
        Assert.Equal(5000,unit.Syllabus!.Length);
        foreach(var code in new[]{"_A","Á","A B",new string('A',31)}) Assert.Throws<CourseRuleException>(()=>unit.Update(1,code,"Nome",null,Now));
        Assert.Throws<CourseRuleException>(()=>unit.Update(1,"OK",new string('N',201),null,Now));
        Assert.Throws<CourseRuleException>(()=>unit.Update(1,"OK","Nome",new string('S',5001),Now));
        Assert.Throws<CourseRuleException>(()=>CourseUnit.Create(1,0,3,Guid.NewGuid(),"A","Nome",null,Now));
        Assert.Throws<CourseRuleException>(()=>unit.Archive(1,DateTime.SpecifyKind(Now,DateTimeKind.Local))); Assert.Equal(1,unit.RowVersion);
        unit.Update(1,"OK","Nome","   ",Now); Assert.Null(unit.Syllabus);
        var parent = Course.Create(1,3,Guid.NewGuid(),"TI","Curso","Texto",Now); parent.TouchUnits(1,Now.AddMinutes(1)); Assert.Equal(2,parent.RowVersion); Assert.True(parent.IsActive); Assert.Equal("Texto",parent.Description); Assert.Null(parent.DeletedAt);
        Assert.Throws<CourseRuleException>(()=>parent.TouchUnits(1,Now)); Assert.Throws<CourseRuleException>(()=>parent.TouchUnits(2,DateTime.SpecifyKind(Now,DateTimeKind.Local)));
        parent.Archive(2,Now); Assert.Equal("CourseArchived",Assert.Throws<CourseRuleException>(()=>parent.TouchUnits(3,Now)).Code);
    }
}
