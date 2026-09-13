using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using PlayStead.UI.Navigation;

namespace PlayStead.UI.Shell;

public sealed class ShellViewModel :
    INotifyPropertyChanged
{
    private readonly NavigationService _navigation;
    private readonly RelayCommand _goBackCommand;

    public ShellViewModel(
        NavigationService navigation)
    {
        ArgumentNullException.ThrowIfNull(
            navigation);

        _navigation =
            navigation;

        NavigateHomeCommand =
            new RelayCommand(
                () => NavigatePrimary(
                    AppRoute.Home));

        NavigateLibraryCommand =
            new RelayCommand(
                () => NavigatePrimary(
                    AppRoute.Library));

        NavigateAttentionCommand =
            new RelayCommand(
                () => NavigatePrimary(
                    AppRoute.Attention));

        NavigateSettingsCommand =
            new RelayCommand(
                () => NavigatePrimary(
                    AppRoute.Settings));

        _goBackCommand =
            new RelayCommand(
                ExecuteGoBack,
                () => CanGoBack);

        GoBackCommand =
            _goBackCommand;

        _navigation.Changed +=
            Navigation_OnChanged;
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public AppRoute CurrentRoute =>
        _navigation.CurrentRoute;

    public bool IsHomeActive =>
        CurrentRoute == AppRoute.Home;

    public bool IsLibraryActive =>
        CurrentRoute == AppRoute.Library;

    public bool IsAttentionActive =>
        CurrentRoute == AppRoute.Attention;

    public bool IsSettingsActive =>
        CurrentRoute == AppRoute.Settings;

    public bool CanGoBack =>
        _navigation.CanGoBack;

    public ICommand NavigateHomeCommand
    {
        get;
    }

    public ICommand NavigateLibraryCommand
    {
        get;
    }

    public ICommand NavigateAttentionCommand
    {
        get;
    }

    public ICommand NavigateSettingsCommand
    {
        get;
    }

    public ICommand GoBackCommand
    {
        get;
    }

    private void NavigatePrimary(
        AppRoute route)
    {
        _navigation.Navigate(
            new NavigationRequest(
                route));
    }

    private void ExecuteGoBack()
    {
        _navigation.GoBack();
    }

    private void Navigation_OnChanged(
        object? sender,
        EventArgs e)
    {
        OnPropertyChanged(
            nameof(CurrentRoute));

        OnPropertyChanged(
            nameof(IsHomeActive));

        OnPropertyChanged(
            nameof(IsLibraryActive));

        OnPropertyChanged(
            nameof(IsAttentionActive));

        OnPropertyChanged(
            nameof(IsSettingsActive));

        OnPropertyChanged(
            nameof(CanGoBack));

        _goBackCommand
            .NotifyCanExecuteChanged();
    }

    private void OnPropertyChanged(
        [CallerMemberName]
        string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}
