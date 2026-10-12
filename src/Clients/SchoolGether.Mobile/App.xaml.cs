namespace SchoolGether.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        UserAppTheme = AppTheme.Light;
        this.services = services;
    }

    // Criar as páginas depois de carregar os recursos globais usados pelo XAML.
    protected override Window CreateWindow(IActivationState? activationState)
        => new(services.GetRequiredService<AppShell>());
}
