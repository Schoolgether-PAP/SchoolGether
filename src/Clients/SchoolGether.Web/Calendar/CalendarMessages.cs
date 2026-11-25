using System.Globalization;
using System.Resources;
namespace SchoolGether.Web.Calendar;

public static class CalendarMessages
{ private static readonly ResourceManager Resources = new("SchoolGether.Web.Calendar.CalendarMessages", typeof(CalendarMessages).Assembly); public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key; }