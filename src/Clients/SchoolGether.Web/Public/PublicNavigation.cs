using Microsoft.AspNetCore.Components;
namespace SchoolGether.Web.Public;
public static class PublicNavigation
{
    public static string Url(NavigationManager navigation, string? view = null, string? plan = null) => navigation.BaseUri
        + (view is null ? "" : "?view=" + Uri.EscapeDataString(view))
        + (plan is null ? "" : "&plan=" + Uri.EscapeDataString(plan));
    public static string View(NavigationManager navigation)
    {
        var query = new Uri(navigation.Uri).Query.TrimStart('?').Split('&');
        var view = query.FirstOrDefault(value => value.StartsWith("view=", StringComparison.Ordinal));
        return view is "view=entrar" or "view=area" or "view=aderir" ? view[5..] : "";
    }
}
