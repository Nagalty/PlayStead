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
    private string _searchQuery = string.Empty;

    public ShellViewModel(
        NavigationService navigation)
    {
        ArgumentNullException.ThrowIfNull(
            navigation);

        _navigation =
            navigation;

        Search =
            new GlobalSearchViewModel(
                navigation);

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

        NavigateAboutCommand =
            new RelayCommand(
                () => NavigatePrimary(
                    AppRoute.About));

        _goBackCommand =
            new RelayCommand(
                ExecuteGoBack,
                () => CanGoBack);

        GoBackCommand =
            _goBackCommand;

        SearchCommand =
            new RelayCommand(
                ExecuteSearch,
                () => !string.IsNullOrWhiteSpace(SearchQuery));

        _navigation.Changed +=
            Navigation_OnChanged;
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public event EventHandler<string>? SearchRequested;

    public GlobalSearchViewModel Search { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetSearchQuery(value);
    }

    public ICommand SearchCommand { get; }

    public void SetSearchQuery(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (_searchQuery == query) return;
        _searchQuery = query;
        Search.SetQuery(query);
        OnPropertyChanged(nameof(SearchQuery));
        ((RelayCommand)SearchCommand).NotifyCanExecuteChanged();
    }

    public void SetSearchItems(IEnumerable<PlayStead.UI.Library.LibraryItemViewModel> items) =>
        Search.SetItems(items);

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

    public bool IsAboutActive =>
        CurrentRoute == AppRoute.About;

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

    public ICommand NavigateAboutCommand
    {
        get;
    }

    private void ExecuteSearch()
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchRequested?.Invoke(this, SearchQuery);
        }
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
            nameof(IsAboutActive));

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
