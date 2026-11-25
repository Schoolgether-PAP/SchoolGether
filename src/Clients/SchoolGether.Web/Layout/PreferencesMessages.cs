using System.Resources;

namespace SchoolGether.Web.Layout;

internal static class PreferencesMessages
{
    private static readonly ResourceManager Resources = new("SchoolGether.Web.Resources.PreferencesMessages", typeof(PreferencesMessages).Assembly);
    public static string Get(string key) => Resources.GetString(key, System.Globalization.CultureInfo.GetCultureInfo("pt-PT")) ?? key;
}
