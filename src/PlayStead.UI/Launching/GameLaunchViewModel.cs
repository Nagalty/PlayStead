using PlayStead.Core.Library;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace PlayStead.UI.Launching;

public sealed class GameLaunchViewModel
{
    private readonly GameLaunchService _launchService;
    private bool _isSessionActive;

    public GameLaunchViewModel(
        GameId gameId,
        IEnumerable<GameInstallation> installations,
        GameLaunchService launchService)
    {
        ArgumentNullException.ThrowIfNull(installations);
        ArgumentNullException.ThrowIfNull(launchService);

        _launchService = launchService;
        _isSessionActive = false;
        var allInstallations = installations.ToArray();

        LaunchOptions =
            allInstallations
                .Where(
                    installation =>
                        installation.GameId == gameId &&
                        _launchService.CanLaunch(installation))
                .ToArray();

        DefaultInstallation =
            GameLaunchInstallationSelector.SelectDefault(
                gameId,
                LaunchOptions);
        PlayCommand = new RelayCommand(() => _ = TryPlayDefault(), () => CanPlay);

        SteamInstallation = allInstallations
            .Where(installation => installation.GameId == gameId)
            .FirstOrDefault(_launchService.CanOpenSteam);
        OpenProviderCommand = new RelayCommand(() => TryOpenProvider(), () => CanOpenProvider);
    }

    public IReadOnlyList<GameInstallation> LaunchOptions { get; }

    public GameInstallation? DefaultInstallation { get; }

    public bool CanPlay => DefaultInstallation is not null && !_isSessionActive;

    public RelayCommand PlayCommand { get; }

    public bool IsSessionActive => _isSessionActive;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetSessionActive(bool isActive)
    {
        if (_isSessionActive == isActive)
            return;

        _isSessionActive = isActive;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSessionActive)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanPlay)));
        PlayCommand.NotifyCanExecuteChanged();
    }

    public bool HasMultipleLaunchOptions => LaunchOptions.Count > 1;

    public GameInstallation? SteamInstallation { get; }

    public bool CanOpenSteam => SteamInstallation is not null;

    public bool CanOpenProvider => CanOpenSteam;

    public string ProviderDisplayName => CanOpenSteam ? "Steam" : string.Empty;

    public ICommand OpenProviderCommand { get; }

    public ICommand OpenSteamCommand => OpenProviderCommand;

    public bool TryPlayDefault()
    {
        return CanPlay &&
            _launchService.TryLaunch(DefaultInstallation!);
    }

    public bool TryPlay(GameInstallation installation)
    {
        return CanPlay && LaunchOptions.Contains(installation) &&
            _launchService.TryLaunch(installation);
    }

    public bool TryOpenSteam() =>
        SteamInstallation is not null &&
        _launchService.TryOpenSteam(SteamInstallation);

    public bool TryOpenProvider() => TryOpenSteam();
}
