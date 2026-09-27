using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using PlayStead.Core.Updates;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Updates;

public sealed class AppUpdateNotificationViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AppUpdateCoordinator _coordinator;
    private readonly AsyncRelayCommand _actionCommand;
    private bool _isVisible;
    private string _title = string.Empty;
    private string _detail = string.Empty;
    private string _actionLabel = string.Empty;
    private AppUpdateState _state;

    public AppUpdateNotificationViewModel(AppUpdateCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _state = coordinator.LastState ?? AppUpdateState.Unknown(DistributionChannel.GitHub, string.Empty);
        _actionCommand = new AsyncRelayCommand(ExecuteActionAsync, CanExecuteAction);
        _coordinator.StateChanged += CoordinatorOnStateChanged;
        ApplyState(_state);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsVisible { get => _isVisible; private set => SetField(ref _isVisible, value); }
    public string Title { get => _title; private set => SetField(ref _title, value); }
    public string Detail { get => _detail; private set => SetField(ref _detail, value); }
    public string ActionLabel { get => _actionLabel; private set => SetField(ref _actionLabel, value); }
    public ICommand ActionCommand => _actionCommand;

    public void Dispose() => _coordinator.StateChanged -= CoordinatorOnStateChanged;

    private void CoordinatorOnStateChanged(object? sender, AppUpdateState state)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(new Action(() => ApplyState(state)));
            return;
        }

        ApplyState(state);
    }

    private void ApplyState(AppUpdateState state)
    {
        _state = state;
        var visible = state.Status is AppUpdateStatus.UpdateAvailable or AppUpdateStatus.Downloading or AppUpdateStatus.ReadyToInstall
            || (state.Status == AppUpdateStatus.Error && state.ExpectedSha256 is not null);
        IsVisible = visible;
        Title = state.Status == AppUpdateStatus.ReadyToInstall
            ? "Mise à jour prête à être installée"
            : state.Status == AppUpdateStatus.Error
                ? "Impossible de préparer la mise à jour."
                : visible ? "Mise à jour PlayStead disponible" : string.Empty;
        Detail = state.Status == AppUpdateStatus.Downloading
            ? state.TotalBytes is > 0
                ? $"Téléchargement de la mise à jour… {state.BytesReceived * 100L / state.TotalBytes.Value} %"
                : "Téléchargement de la mise à jour…"
            : visible && state.Status != AppUpdateStatus.Error
                ? state.Channel == DistributionChannel.MicrosoftStore
                ? "Une nouvelle version t’attend sur le Microsoft Store."
                : state.AvailableVersion is { Length: > 0 } version
                    ? $"{version} est prête."
                    : "Une nouvelle version est prête."
            : string.Empty;
        ActionLabel = state.Status == AppUpdateStatus.Downloading
            ? "Téléchargement…"
            : state.Status == AppUpdateStatus.ReadyToInstall
                ? "Installer et redémarrer"
                : visible && state.Status != AppUpdateStatus.Error
            ? state.Action == AppUpdateActionKind.OpenMicrosoftStore
                ? "Ouvrir Microsoft Store"
                : "Mettre à jour"
            : string.Empty;
        _actionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ActionCommand));
    }

    private bool CanExecuteAction() =>
        ((_state.Status == AppUpdateStatus.ReadyToInstall && _state.Channel == DistributionChannel.GitHub && _coordinator.CanInstall)
         || (_state.Status == AppUpdateStatus.UpdateAvailable && _state.Action == AppUpdateActionKind.OpenMicrosoftStore && _state.ActionUri is not null)
         || (_state.Channel == DistributionChannel.GitHub && _state.Action == AppUpdateActionKind.DownloadAndInstall && _coordinator.CanDownload));

    private async Task ExecuteActionAsync()
    {
        if (!CanExecuteAction())
            return;

        if (_state.Status == AppUpdateStatus.ReadyToInstall)
        {
            _coordinator.StartInstallation();
            return;
        }

        if (_state.Action == AppUpdateActionKind.OpenMicrosoftStore && _state.ActionUri is { } uri)
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return;
        }

        _coordinator.StartGitHubDownload();
        if (_coordinator.CurrentDownload is { } task)
            await task.ConfigureAwait(false);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
