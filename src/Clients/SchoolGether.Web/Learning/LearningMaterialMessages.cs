using System.Globalization;
using System.Resources;
namespace SchoolGether.Web.Learning;
internal static class LearningMaterialMessages
{
    static readonly ResourceManager Manager = new("SchoolGether.Web.Learning.LearningMaterialMessages", typeof(LearningMaterialMessages).Assembly);
    public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
