using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;

namespace PlayStead.UI.Settings;

public sealed class SettingsViewModel :
    INotifyPropertyChanged
{
    private readonly UiPreferencesStore _store;
    private bool _reduceMotion;

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
    }

    public Task SaveAsync(
        CancellationToken cancellationToken)
    {
        return _store.SaveAsync(
            new UiPreferences(ReduceMotion),
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
