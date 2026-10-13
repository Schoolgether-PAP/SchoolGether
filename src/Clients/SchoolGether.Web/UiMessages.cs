using System.Globalization;
using System.Resources;

namespace SchoolGether.Web;

public static class UiMessages
{
    private static readonly ResourceManager Resources = new("SchoolGether.Web.Resources.UiMessages", typeof(UiMessages).Assembly);
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-PT");

    public static string Get(string key) => Resources.GetString(key, Culture) ?? key;
    public static string Format(string key, params object[] arguments)
        => string.Format(Culture, Get(key), arguments);
}
