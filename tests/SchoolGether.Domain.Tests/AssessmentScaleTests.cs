using SchoolGether.Domain.Learning;

namespace SchoolGether.Domain.Tests;

public sealed class AssessmentScaleTests
{
    [Theory]
    [InlineData(EducationPathway.Primary, 1, AssessmentScale.PrimaryMention)]
    [InlineData(EducationPathway.Primary, 4, AssessmentScale.PrimaryMention)]
    [InlineData(EducationPathway.Basic, 5, AssessmentScale.Level1To5)]
    [InlineData(EducationPathway.Basic, 9, AssessmentScale.Level1To5)]
    [InlineData(EducationPathway.SecondaryGeneral, 10, AssessmentScale.Values0To20)]
    [InlineData(EducationPathway.SecondaryProfessional, 1, AssessmentScale.Values0To20)]
    [InlineData(EducationPathway.HigherEducation, 1, AssessmentScale.Values0To20)]
    [InlineData(EducationPathway.AdultEfa, 1, AssessmentScale.PassFail)]
    [InlineData(EducationPathway.ModularTraining, 1, AssessmentScale.PassFail)]
    [InlineData(EducationPathway.Preschool, 1, AssessmentScale.FeedbackOnly)]
    public void Recommendations_use_pathway_instead_of_institution_name_or_year_alone(EducationPathway pathway, int year, AssessmentScale expected)
        => Assert.Equal(expected, AssessmentPolicy.Recommend(pathway, year));

    [Theory]
    [InlineData(EducationPathway.Primary, 5)]
    [InlineData(EducationPathway.Basic, 4)]
    [InlineData(EducationPathway.SecondaryGeneral, 9)]
    public void Contradictory_pathway_year_needs_correction(EducationPathway pathway, int year)
        => Assert.Throws<ArgumentException>(() => AssessmentPolicy.Recommend(pathway, year));

    [Theory]
    [InlineData(AssessmentScale.Level1To5, 0)]
    [InlineData(AssessmentScale.Level1To5, 6)]
    [InlineData(AssessmentScale.Values0To20, 21)]
    [InlineData(AssessmentScale.Percentage, 101)]
    public void Numeric_marks_cannot_exceed_the_selected_scale(AssessmentScale scale, int value)
        => Assert.Throws<ArgumentException>(() => AssessmentMark.Validate(scale, value, null, "Comentário"));

    [Fact]
    public void Descriptive_and_qualitative_records_do_not_implicitly_convert_percentages()
    {
        AssessmentMark.Validate(AssessmentScale.PrimaryMention, null, "VeryGood", "Bom progresso.");
        AssessmentMark.Validate(AssessmentScale.PassFail, null, "Achieved", "Objetivos atingidos.");
        AssessmentMark.Validate(AssessmentScale.FeedbackOnly, null, null, "Continuar a praticar.");
        Assert.Throws<ArgumentException>(() => AssessmentMark.Validate(AssessmentScale.PrimaryMention, 90m, "VeryGood", ""));
        Assert.Throws<ArgumentException>(() => AssessmentMark.Validate(AssessmentScale.Level1To5, 3.5m, null, ""));
        Assert.Throws<ArgumentException>(() => AssessmentMark.Validate(AssessmentScale.FeedbackOnly, null, null, " "));
        Assert.False(AssessmentPolicy.Allows(EducationPathway.Preschool, AssessmentScale.Values0To20));
        Assert.True(AssessmentPolicy.Allows(EducationPathway.Primary, AssessmentScale.FeedbackOnly));
    }
}
