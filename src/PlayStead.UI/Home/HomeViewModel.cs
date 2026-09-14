using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Home;

public sealed class HomeViewModel :
    INotifyPropertyChanged
{
    private readonly LibraryViewModel _libraryViewModel;
    private readonly SessionViewModel _sessionViewModel;
    private readonly NavigationService _navigationService;

    public HomeViewModel(
        LibraryViewModel libraryViewModel,
        SessionViewModel sessionViewModel,
        NavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(libraryViewModel);
        ArgumentNullException.ThrowIfNull(sessionViewModel);
        ArgumentNullException.ThrowIfNull(navigationService);

        _libraryViewModel = libraryViewModel;
        _sessionViewModel = sessionViewModel;
        _navigationService = navigationService;

        NavigateLibraryCommand =
            new RelayCommand(
                () => _navigationService.Navigate(
                    new NavigationRequest(AppRoute.Library)));

        NavigateSessionsCommand =
            new RelayCommand(
                () => _navigationService.Navigate(
                    new NavigationRequest(AppRoute.Sessions)));

        _libraryViewModel.PropertyChanged += LibraryViewModel_OnPropertyChanged;
        _sessionViewModel.PropertyChanged += SessionViewModel_OnPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int LibraryGameCount =>
        _libraryViewModel.Items.Count;

    public IReadOnlyList<ActiveSessionItemViewModel> ActiveSessions =>
        _sessionViewModel.ActiveSessions;

    public IReadOnlyList<RecentSessionItemViewModel> RecentSessions =>
        _sessionViewModel.RecentSessions;

    public bool HasRecentActivity =>
        _sessionViewModel.HasRecentSessions;

    public ICommand NavigateLibraryCommand { get; }

    public ICommand NavigateSessionsCommand { get; }

    private void LibraryViewModel_OnPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.Items))
        {
            OnPropertyChanged(nameof(LibraryGameCount));
        }
    }

    private void SessionViewModel_OnPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SessionViewModel.ActiveSessions):
                OnPropertyChanged(nameof(ActiveSessions));
                break;
            case nameof(SessionViewModel.RecentSessions):
                OnPropertyChanged(nameof(RecentSessions));
                break;
            case nameof(SessionViewModel.HasRecentSessions):
                OnPropertyChanged(nameof(HasRecentActivity));
                break;
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
