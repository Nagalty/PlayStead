using System.IO;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.UI.Launching;

public sealed class GameLaunchService
{
    private readonly IExternalUriLauncher
        _externalUriLauncher;
    private readonly ILocalProcessLauncher _localProcessLauncher;
    private readonly IManualSessionLaunchSink? _sessionLaunchSink;
    private readonly ProviderInstallUpdateStateReconciliationService? _installUpdates;

    public GameLaunchService(
        IExternalUriLauncher externalUriLauncher,
        ILocalProcessLauncher? localProcessLauncher = null,
        IManualSessionLaunchSink? sessionLaunchSink = null,
        ProviderInstallUpdateStateReconciliationService? installUpdates = null)
    {
        ArgumentNullException.ThrowIfNull(
            externalUriLauncher);

        _externalUriLauncher =
            externalUriLauncher;
        _localProcessLauncher = localProcessLauncher ?? new LocalProcessLauncher();
        _sessionLaunchSink = sessionLaunchSink;
        _installUpdates = installUpdates;
    }

    public bool CanLaunch(
        GameInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(
            installation);

        var update = _installUpdates?.GetAll().FirstOrDefault(value =>
            value.GameId == installation.GameId && value.Provider == installation.Provider);
        if (update is not null &&
            (update.Status == ProviderInstallUpdateStatus.Downloading ||
             update.Status == ProviderInstallUpdateStatus.Staging))
            return false;

        return installation.Provider switch
        {
            ProviderKind.Manual => CanLaunchLocal(installation),
            ProviderKind.Gog => CanLaunchGog(installation),
            ProviderKind.Steam => SteamLaunchUriFactory.CreateOrNull(installation) is not null,
            ProviderKind.Epic => EpicLaunchUriFactory.CreateOrNull(installation) is not null,
            _ => false
        };
    }

    public bool TryLaunch(
        GameInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(
            installation);

        if (!CanLaunch(installation))
            return false;

        if (installation.Provider == ProviderKind.Manual)
            return TryLaunchLocal(installation);

        if (installation.Provider == ProviderKind.Gog)
            return TryLaunchGog(installation);

        var uri = installation.Provider switch
        {
            ProviderKind.Steam => SteamLaunchUriFactory.CreateOrNull(installation),
            ProviderKind.Epic => EpicLaunchUriFactory.CreateOrNull(installation),
            _ => null
        };

        if (uri is null)
        {
            return false;
        }

        _externalUriLauncher.Open(
            uri);

        return true;
    }

    private bool TryLaunchLocal(GameInstallation installation)
    {
        if (_localProcessLauncher is IProcessIdentityLauncher identityLauncher)
        {
            var identity = identityLauncher.StartWithIdentity(
                installation.ExecutablePath!,
                installation.WorkingDirectory!,
                installation.LaunchArguments);
            if (identity is null)
                return false;

            _sessionLaunchSink?.TrackLaunchedProcess(
                installation.GameId.Value,
                identity.ProcessId,
                identity.StartedAtUtc);
            return true;
        }

        return _localProcessLauncher.Start(
            installation.ExecutablePath!,
            installation.WorkingDirectory!,
            installation.LaunchArguments);
    }

    private bool TryLaunchGog(GameInstallation installation) => TryLaunchLocal(installation);

    private static bool CanLaunchGog(GameInstallation installation) => CanLaunchLocal(installation);

    public bool CanOpenSteam(GameInstallation installation) =>
        SteamLaunchUriFactory.CreateStoreOrNull(installation) is not null;

    public bool TryOpenSteam(GameInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        var uri = SteamLaunchUriFactory.CreateStoreOrNull(installation);
        if (uri is null) return false;
        _externalUriLauncher.Open(uri);
        return true;
    }

    private static bool CanLaunchLocal(GameInstallation installation) =>
        installation.IsPresent &&
        File.Exists(installation.ExecutablePath) &&
        Directory.Exists(installation.WorkingDirectory);
}
