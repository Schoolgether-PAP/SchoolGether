namespace SchoolGether.Mobile;

public partial class AppShell : Shell
{
    public AppShell(MainPage page)
    {
        InitializeComponent();
        IdentityContent.Content = page;
    }
}
