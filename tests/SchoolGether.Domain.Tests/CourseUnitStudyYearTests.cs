using SchoolGether.Domain.Academic;
namespace SchoolGether.Domain.Tests;
public sealed class CourseUnitStudyYearTests
{
    private static readonly DateTime Now = new(2026,10,20,12,0,0,DateTimeKind.Utc);
    [Fact]
    public void Legacy_fingerprint_and_replay_survive_assigning_year_and_omitted_update()
    {
        var key = Guid.NewGuid(); var unit = CourseUnit.Create(1,2,3,key,"MAT","Matemática",null,Now);
        // SHA256 do JSON UTF8 ["MAT","Matem\\u00E1tica",null], contrato v1.
        var legacy = unit.CreationFingerprint;
        unit.Update(1,"MAT","Matemática",null,Now,10); unit.Update(2,"M10","Novo",null,Now);
        Assert.Equal(10,unit.StudyYear); Assert.Equal(legacy,unit.CreationFingerprint);
        Assert.True(unit.IsReplayOf(CourseUnit.Create(1,2,3,key,"MAT","Matemática",null,Now)));
        Assert.False(unit.IsReplayOf(CourseUnit.Create(1,2,3,key,"MAT","Matemática",null,Now,10)));
    }
    [Theory]
    [InlineData(0)] [InlineData(21)]
    public void Invalid_year_does_not_mutate_unit(int year)
    {
        var unit = CourseUnit.Create(1,2,3,Guid.NewGuid(),"MAT","Nome",null,Now,10);
        Assert.Throws<CourseRuleException>(()=>unit.Update(1,"MAT","Perdido",null,Now,year));
        Assert.Equal(1,unit.RowVersion); Assert.Equal("Nome",unit.Name); Assert.Equal(10,unit.StudyYear);
    }
}
