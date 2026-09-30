using System.IO;
using PlayStead.Core.Library;

namespace PlayStead.UI.Launching;

public sealed class GameLaunchService
{
    private readonly IExternalUriLauncher
        _externalUriLauncher;
    private readonly ILocalProcessLauncher _localProcessLauncher;
    private readonly IManualSessionLaunchSink? _sessionLaunchSink;

    public GameLaunchService(
        IExternalUriLauncher externalUriLauncher,
        ILocalProcessLauncher? localProcessLauncher = null,
        IManualSessionLaunchSink? sessionLaunchSink = null)
    {
        ArgumentNullException.ThrowIfNull(
            externalUriLauncher);

        _externalUriLauncher =
            externalUriLauncher;
        _localProcessLauncher = localProcessLauncher ?? new LocalProcessLauncher();
        _sessionLaunchSink = sessionLaunchSink;
    }

    public bool CanLaunch(
        GameInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(
            installation);

        return installation.Provider == ProviderKind.Manual
            ? installation.IsPresent &&
              File.Exists(installation.ExecutablePath) &&
              Directory.Exists(installation.WorkingDirectory)
            : SteamLaunchUriFactory.CreateOrNull(installation) is not null;
    }

    public bool TryLaunch(
        GameInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(
            installation);

        if (installation.Provider == ProviderKind.Manual)
        {
            if (!CanLaunch(installation))
                return false;

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

        var uri = SteamLaunchUriFactory.CreateOrNull(installation);

        if (uri is null)
        {
            return false;
        }

        _externalUriLauncher.Open(
            uri);

        return true;
    }

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
}
