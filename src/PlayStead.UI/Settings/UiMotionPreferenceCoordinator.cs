using System.ComponentModel;
using System.Windows;

namespace PlayStead.UI.Settings;

public sealed class UiMotionPreferenceCoordinator
{
    private readonly SettingsViewModel _settingsViewModel;
    private readonly UiMotionController _motionController;
    private readonly ResourceDictionary _resources;
    private Task? _initializationTask;

    public UiMotionPreferenceCoordinator(
        SettingsViewModel settingsViewModel,
        UiMotionController motionController,
        ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(settingsViewModel);
        ArgumentNullException.ThrowIfNull(motionController);
        ArgumentNullException.ThrowIfNull(resources);

        _settingsViewModel = settingsViewModel;
        _motionController = motionController;
        _resources = resources;
    }

    public Task InitializeAsync(
        CancellationToken cancellationToken)
    {
        return _initializationTask ??= InitializeCoreAsync(cancellationToken);
    }

    private async Task InitializeCoreAsync(
        CancellationToken cancellationToken)
    {
        await _settingsViewModel.LoadAsync(cancellationToken);

        _motionController.Apply(_resources, _settingsViewModel.ReduceMotion);

        _settingsViewModel.PropertyChanged += SettingsViewModel_OnPropertyChanged;
    }

    private void SettingsViewModel_OnPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ReduceMotion))
        {
            _motionController.Apply(_resources, _settingsViewModel.ReduceMotion);
        }
    }
}
