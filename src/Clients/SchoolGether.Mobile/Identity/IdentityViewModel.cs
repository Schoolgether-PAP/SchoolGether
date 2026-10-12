using System.Net;
using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolGether.ApiClient.Identity;
using SchoolGether.Contracts.Identity;

namespace SchoolGether.Mobile.Identity;

public sealed class IdentityViewModel : ObservableObject
{
    private readonly IdentitySession session;
    private readonly Action<Action> dispatch;
    private readonly string? configurationError;
    private int running;
    private string email = "", password = "";
    private string? error, statusMessage;
    private bool isBusy, showPassword, canRetryRestore, canRetryLogout;

    public IdentityViewModel(IdentitySession session, Action<Action>? dispatch = null, string? configurationError = null)
    {
        this.session = session;
        this.dispatch = dispatch ?? (action => action());
        this.configurationError = configurationError;
        error = configurationError;
        LoginCommand = new AsyncRelayCommand(() => RunAsync("login"), () => CanInteract && !IsAuthenticated);
        RestoreCommand = new AsyncRelayCommand(() => RunAsync("restore"), () => CanInteract && !IsAuthenticated);
        RefreshProfileCommand = new AsyncRelayCommand(() => RunAsync("profile"), () => CanInteract && IsAuthenticated);
        LogoutCommand = new AsyncRelayCommand(() => RunAsync("logout"), () => !IsBusy && (IsAuthenticated || CanRetryLogout));
        TogglePasswordCommand = new RelayCommand(() => ShowPassword = !ShowPassword);
        session.Changed += () => this.dispatch(PublishAccount);
    }

    public string Email { get => email; set => SetProperty(ref email, value); }
    public string Password { get => password; set => SetProperty(ref password, value); }
    public string? Error { get => error; private set { SetProperty(ref error, value); OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(Error);
    public string? StatusMessage { get => statusMessage; private set { SetProperty(ref statusMessage, value); OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);
    public bool IsBusy { get => isBusy; private set { SetProperty(ref isBusy, value); OnPropertyChanged(nameof(CanInteract)); NotifyCommands(); } }
    public bool CanInteract => !IsBusy && configurationError is null && !CanRetryLogout;
    public bool CanRetryLogout { get => canRetryLogout; private set { SetProperty(ref canRetryLogout, value); OnPropertyChanged(nameof(CanInteract)); NotifyCommands(); } }
    public bool CanRetryRestore { get => canRetryRestore; private set => SetProperty(ref canRetryRestore, value); }
    public bool ShowPassword
    {
        get => showPassword;
        set { SetProperty(ref showPassword, value); OnPropertyChanged(nameof(IsPasswordHidden)); OnPropertyChanged(nameof(PasswordToggleText)); }
    }
    public bool IsPasswordHidden => !ShowPassword;
    public string PasswordToggleText => ShowPassword ? UiMessages.HidePassword : UiMessages.ShowPassword;
    public AccountResponse? Account => session.Account;
    public bool IsAuthenticated => Account is not null;
    public bool IsLoginVisible => !IsAuthenticated;
    public string RoleText => Account?.Role switch
    {
        "PlatformAdmin" => UiMessages.PlatformAdmin,
        "InstitutionAdmin" => UiMessages.InstitutionAdmin,
        "Teacher" => UiMessages.Teacher,
        "Student" => UiMessages.Student,
        _ => UiMessages.UnknownRole
    };
    public string InstitutionText => Account?.InstitutionId is int id
        ? string.Format(System.Globalization.CultureInfo.CurrentCulture, UiMessages.InstitutionFormat, id)
        : UiMessages.PlatformAdministration;
    public IAsyncRelayCommand LoginCommand { get; }
    public IAsyncRelayCommand RestoreCommand { get; }
    public IAsyncRelayCommand RefreshProfileCommand { get; }
    public IAsyncRelayCommand LogoutCommand { get; }
    public IRelayCommand TogglePasswordCommand { get; }

    private async Task RunAsync(string operation)
    {
        if (configurationError is not null || Interlocked.CompareExchange(ref running, 1, 0) != 0) return;
        dispatch(() => { IsBusy = true; Error = null; StatusMessage = null; CanRetryRestore = false; });
        try
        {
            switch (operation)
            {
                case "login":
                    var normalizedEmail = Email.Trim();
                    if (normalizedEmail.Length > 256 || !MailAddress.TryCreate(normalizedEmail, out var address)
                        || address.Address != normalizedEmail || string.IsNullOrEmpty(Password) || Password.Length > 128)
                    {
                        dispatch(() => Error = UiMessages.RequiredFields);
                        return;
                    }
                    await session.SignInAsync(new LoginRequest(normalizedEmail, Password));
                    break;
                case "restore": await session.RestoreAsync(); break;
                case "profile": await session.GetProfileAsync(); break;
                case "logout":
                    var revoked = await session.SignOutAsync();
                    dispatch(() => { CanRetryLogout = false; StatusMessage = revoked ? UiMessages.LogoutConfirmed : UiMessages.LogoutLocal; });
                    break;
            }
        }
        catch (SessionExpiredException)
        {
            dispatch(() => Error = operation == "login" ? UiMessages.InvalidLogin : UiMessages.SessionExpired);
        }
        catch (IdentityApiException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
        {
            dispatch(() => { Error = UiMessages.RateLimited; CanRetryRestore = operation == "restore"; });
        }
        catch (HttpRequestException)
        {
            dispatch(() => { Error = UiMessages.NetworkError; CanRetryRestore = operation == "restore"; });
        }
        catch (OperationCanceledException)
        {
            dispatch(() => { Error = UiMessages.NetworkError; CanRetryRestore = operation == "restore"; });
        }
        catch (IOException)
        {
            dispatch(() => { Error = operation == "logout" ? UiMessages.LogoutStorageError : UiMessages.StorageError; CanRetryLogout = operation == "logout"; CanRetryRestore = operation == "restore"; });
        }
        catch (Exception)
        {
            // Nunca apresentar detalhes da resposta HTTP, credenciais ou exceções de plataforma.
            dispatch(() => { Error = UiMessages.RequestError; CanRetryRestore = operation == "restore"; });
        }
        finally
        {
            dispatch(() => { Password = ""; ShowPassword = false; PublishAccount(); IsBusy = false; });
            Interlocked.Exchange(ref running, 0);
        }
    }

    private void PublishAccount()
    {
        OnPropertyChanged(nameof(Account));
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(IsLoginVisible));
        OnPropertyChanged(nameof(RoleText));
        OnPropertyChanged(nameof(InstitutionText));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        LoginCommand.NotifyCanExecuteChanged(); RestoreCommand.NotifyCanExecuteChanged();
        RefreshProfileCommand.NotifyCanExecuteChanged(); LogoutCommand.NotifyCanExecuteChanged();
    }
}
