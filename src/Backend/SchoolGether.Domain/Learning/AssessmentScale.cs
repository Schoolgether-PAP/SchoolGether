namespace SchoolGether.Domain.Learning;

public enum EducationPathway
{
    Preschool, Primary, Basic, SecondaryGeneral, SecondaryProfessional,
    HigherEducation, AdultEfa, ModularTraining, CefBasic, CefSecondary, Other
}

public enum AssessmentScale { FeedbackOnly, PrimaryMention, Level1To5, Values0To20, PassFail, Percentage }

// Reference scales for learning activities. Formal certification remains the institution's responsibility.
public static class AssessmentPolicy
{
    public static AssessmentScale Recommend(EducationPathway pathway, int studyYear)
    {
        if (!Enum.IsDefined(pathway) || studyYear is < 1 or > 20 ||
            pathway == EducationPathway.Primary && studyYear > 4 ||
            pathway == EducationPathway.Basic && studyYear is < 5 or > 9 ||
            pathway == EducationPathway.SecondaryGeneral && studyYear is < 10 or > 12 ||
            pathway == EducationPathway.SecondaryProfessional && studyYear is not (1 or 2 or 3 or 10 or 11 or 12))
            throw new ArgumentException("InvalidAssessmentPathway");

        return pathway switch
        {
            EducationPathway.Primary => AssessmentScale.PrimaryMention,
            EducationPathway.Basic or EducationPathway.CefBasic => AssessmentScale.Level1To5,
            EducationPathway.SecondaryGeneral or EducationPathway.SecondaryProfessional or
                EducationPathway.HigherEducation or EducationPathway.CefSecondary => AssessmentScale.Values0To20,
            EducationPathway.AdultEfa or EducationPathway.ModularTraining => AssessmentScale.PassFail,
            _ => AssessmentScale.FeedbackOnly
        };
    }

    public static bool Allows(EducationPathway pathway, AssessmentScale scale)
    {
        if (!Enum.IsDefined(pathway) || !Enum.IsDefined(scale)) return false;
        if (scale == AssessmentScale.FeedbackOnly) return true;
        if (pathway == EducationPathway.Preschool) return false;
        // A percentage may be used on an instrument; no conversion to a formal final grade is inferred.
        if (scale == AssessmentScale.Percentage || pathway == EducationPathway.Other) return true;
        return (pathway, scale) switch
        {
            (EducationPathway.Primary, AssessmentScale.PrimaryMention) => true,
            (EducationPathway.Basic or EducationPathway.CefBasic, AssessmentScale.Level1To5) => true,
            (EducationPathway.SecondaryGeneral or EducationPathway.SecondaryProfessional or EducationPathway.HigherEducation or EducationPathway.CefSecondary, AssessmentScale.Values0To20) => true,
            (EducationPathway.AdultEfa or EducationPathway.ModularTraining, AssessmentScale.PassFail) => true,
            _ => false
        };
    }
}

public static class AssessmentMark
{
    public static void Validate(AssessmentScale scale, decimal? number, string? mention, string? feedback)
    {
        if (!Enum.IsDefined(scale) || feedback?.Length > 4000)
            throw new ArgumentException("InvalidAssessmentMark");
        var valid = scale switch
        {
            AssessmentScale.FeedbackOnly => number is null && string.IsNullOrEmpty(mention) && !string.IsNullOrWhiteSpace(feedback),
            AssessmentScale.PrimaryMention => number is null && mention is "Insufficient" or "Sufficient" or "Good" or "VeryGood",
            AssessmentScale.PassFail => number is null && mention is "Achieved" or "NotAchieved",
            AssessmentScale.Level1To5 => number is >= 1 and <= 5 && decimal.Truncate(number.Value) == number.Value && string.IsNullOrEmpty(mention),
            AssessmentScale.Values0To20 => number is >= 0 and <= 20 && decimal.Round(number.Value, 2) == number.Value && string.IsNullOrEmpty(mention),
            AssessmentScale.Percentage => number is >= 0 and <= 100 && decimal.Round(number.Value, 2) == number.Value && string.IsNullOrEmpty(mention),
            _ => false
        };
        if (!valid) throw new ArgumentException("InvalidAssessmentMark");
    }
}
