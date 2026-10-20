using SchoolGether.Domain.Academic;

namespace SchoolGether.Domain.Tests;

public sealed class CourseTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    [Theory]
    [InlineData("")]
    [InlineData("_A")]
    [InlineData("ÁBC")]
    [InlineData("A B")]
    [InlineData("A\nB")]
    [InlineData("Ａ")]
    [InlineData("1234567890123456789012345678901")]
    public void Code_rejects_non_ascii_invalid_first_character_and_excess_length(string code) =>
        Assert.Throws<CourseRuleException>(() => Course.Create(1, 2, Guid.NewGuid(), code, "Curso", null, Now));
    [Fact]
    public void Normalized_creation_replay_survives_edits_archive_and_restore_but_binds_author_and_payload()
    {
        var key = Guid.NewGuid(); var course = Course.Create(1, 2, key, " a.b-c_1 ", " Nome ", "  ", Now);
        Assert.Equal("A.B-C_1", course.Code); Assert.Equal("Nome", course.Name); Assert.Null(course.Description);
        var fingerprint = course.CreationFingerprint;
        course.Update(1, "Z", "Alterado", " detalhe ", Now.AddMinutes(1)); course.Archive(2, Now.AddMinutes(2));
        Assert.True(course.IsReplayOf(Course.Create(1, 2, key, "A.B-C_1", "Nome", null, Now)));
        Assert.False(course.IsReplayOf(Course.Create(1, 3, key, "A.B-C_1", "Nome", null, Now)));
        Assert.False(course.IsReplayOf(Course.Create(1, 2, key, "Z", "Alterado", "detalhe", Now)));
        Assert.Equal(fingerprint, course.CreationFingerprint); Assert.Equal(Now, course.CreatedAt); Assert.Equal(2, course.CreatedBy);
        var refused = Assert.Throws<CourseRuleException>(() => course.Update(3, "Y", "Vedado", null, Now.AddMinutes(3)));
        Assert.Equal("CourseArchived", refused.Code); Assert.Equal(3, course.RowVersion); Assert.Equal("Z", course.Code);
        course.Restore(3, Now.AddMinutes(3)); Assert.Null(course.DeletedAt); Assert.True(course.IsActive); Assert.Equal(4, course.RowVersion);
        Assert.Equal("VersionConflict", Assert.Throws<CourseRuleException>(() => course.Archive(3, Now)).Code);
    }
    [Fact]
    public void Invalid_fields_and_metadata_do_not_partially_change_a_course()
    {
        var course = Course.Create(1, 2, Guid.NewGuid(), new string('a',30), new string('N',200), new string('D',1000), Now);
        Assert.Throws<CourseRuleException>(() => course.Update(1, "VALID", " ", null, Now));
        Assert.Equal(new string('A',30), course.Code); Assert.Equal(1, course.RowVersion);
        Assert.Throws<CourseRuleException>(() => Course.Create(0, 2, Guid.NewGuid(), "A", "B", null, Now));
        Assert.Throws<CourseRuleException>(() => Course.Create(1, 2, Guid.Empty, "A", "B", null, Now));
        Assert.Throws<CourseRuleException>(() => Course.Create(1, 2, Guid.NewGuid(), "A", new string('B',201), null, Now));
        Assert.Throws<CourseRuleException>(() => Course.Create(1, 2, Guid.NewGuid(), "A", "B", new string('D',1001), Now));
        Assert.Throws<CourseRuleException>(() => course.Archive(1, DateTime.SpecifyKind(Now, DateTimeKind.Local)));
        Assert.Equal(1, course.RowVersion); Assert.True(course.IsActive);
    }
}
