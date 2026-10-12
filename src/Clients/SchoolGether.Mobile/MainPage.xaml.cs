using System.ComponentModel;
using SchoolGether.Mobile.Identity;

namespace SchoolGether.Mobile;

public partial class MainPage : ContentPage
{
    private readonly IdentityViewModel model;
    private bool restoreAttempted;
    private bool? wideLayout;

    public MainPage(IdentityViewModel model)
    {
        InitializeComponent();
        this.model = model;
        BindingContext = model;
        model.PropertyChanged += OnIdentityChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (restoreAttempted) return;
        restoreAttempted = true;
        await model.RestoreCommand.ExecuteAsync(null);
    }

    private void OnLayoutSizeChanged(object? sender, EventArgs e)
    {
        var wide = IdentityLayout.Width >= 850;
        if (wideLayout == wide) return;
        wideLayout = wide;
        IdentityLayout.ColumnDefinitions.Clear();
        IdentityLayout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        if (wide) IdentityLayout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetRow(FormPanel, wide ? 0 : 1);
        Grid.SetColumn(FormPanel, wide ? 1 : 0);
    }

    private async void OnPasswordCompleted(object? sender, EventArgs e)
    {
        if (model.LoginCommand.CanExecute(null)) await model.LoginCommand.ExecuteAsync(null);
    }

    private void OnIdentityChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Também protege anúncios se uma notificação for originada fora de um comando da página.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (e.PropertyName == nameof(model.Error) && model.HasError)
                SemanticScreenReader.Default.Announce(model.Error!);
            else if (e.PropertyName == nameof(model.StatusMessage) && model.HasStatus)
                SemanticScreenReader.Default.Announce(model.StatusMessage!);
        });
    }
}
