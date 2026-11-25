using System.Globalization;
using System.Resources;
namespace SchoolGether.Web.Learning;
internal static class LessonPlanMessages
{
    static readonly ResourceManager Manager = new("SchoolGether.Web.Learning.LessonPlanMessages", typeof(LessonPlanMessages).Assembly);
    public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
