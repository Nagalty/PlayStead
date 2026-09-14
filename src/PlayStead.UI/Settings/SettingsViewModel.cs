using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using PlayStead.UI.Library;

namespace PlayStead.UI.Settings;

public sealed class SettingsViewModel :
    INotifyPropertyChanged
{
    private readonly UiPreferencesStore _store;
    private bool _reduceMotion;
    private LibraryViewMode _libraryViewMode = LibraryViewMode.Grid;
    private string _librarySortKey = "Title";
    private string? _libraryFilterKey;

    public SettingsViewModel(
        UiPreferencesStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;

        SaveCommand =
            new AsyncRelayCommand(
                SaveAsync);
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public bool ReduceMotion
    {
        get => _reduceMotion;
        set
        {
            if (_reduceMotion == value)
            {
                return;
            }

            _reduceMotion = value;

            OnPropertyChanged();
        }
    }

    public LibraryViewMode LibraryViewMode
    {
        get => _libraryViewMode;
        set
        {
            if (_libraryViewMode == value)
            {
                return;
            }

            _libraryViewMode = value;
            OnPropertyChanged();
        }
    }

    public IAsyncRelayCommand SaveCommand
    {
        get;
    }

    public async Task LoadAsync(
        CancellationToken cancellationToken)
    {
        var preferences =
            await _store.LoadAsync(
                cancellationToken);

        ReduceMotion = preferences.ReduceMotion;
        LibraryViewMode = preferences.LibraryViewMode;
        _librarySortKey = preferences.LibrarySortKey;
        _libraryFilterKey = preferences.LibraryFilterKey;
    }

    public Task SaveAsync(
        CancellationToken cancellationToken)
    {
        return _store.SaveAsync(
            new UiPreferences(
                ReduceMotion,
                LibraryViewMode,
                _librarySortKey,
                _libraryFilterKey),
            cancellationToken);
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
