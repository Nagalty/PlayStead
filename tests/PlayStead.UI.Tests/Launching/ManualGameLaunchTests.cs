using PlayStead.Core.Library;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Tests.Launching;

public sealed class ManualGameLaunchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-ManualLaunch-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Manual_installation_launches_with_working_directory_and_arguments()
    {
        Directory.CreateDirectory(_root);
        var executable = Path.Combine(_root, "game.exe");
        File.WriteAllText(executable, string.Empty);
        var launcher = new RecordingProcessLauncher();
        var service = new GameLaunchService(new RecordingUriLauncher(), launcher);
        var installation = new GameInstallation(InstallationId.New(), GameId.New(), ProviderKind.Manual,
            "manual:stable", _root, null, true, true, DateTimeOffset.UtcNow,
            InstallationContentKind.Game, executable, _root, "--profile test");

        Assert.True(service.CanLaunch(installation));
        Assert.True(service.TryLaunch(installation));
        Assert.Equal(executable, launcher.ExecutablePath);
        Assert.Equal(_root, launcher.WorkingDirectory);
        Assert.Equal("--profile test", launcher.Arguments);
    }

    [Fact]
    public void Manual_launch_forwards_the_stable_game_id_and_process_identity()
    {
        Directory.CreateDirectory(_root);
        var executable = Path.Combine(_root, "game.exe");
        File.WriteAllText(executable, string.Empty);
        var launcher = new IdentityProcessLauncher();
        var sink = new RecordingSessionLaunchSink();
        var service = new GameLaunchService(new RecordingUriLauncher(), launcher, sink);
        var gameId = GameId.New();
        var installation = new GameInstallation(InstallationId.New(), gameId, ProviderKind.Manual,
            "manual:stable", _root, null, true, true, DateTimeOffset.UtcNow,
            InstallationContentKind.Game, executable, _root, null);

        Assert.True(service.TryLaunch(installation));
        Assert.Equal(gameId.Value, sink.GameId);
        Assert.Equal(launcher.Identity, sink.Identity);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class RecordingProcessLauncher : ILocalProcessLauncher
    {
        public string? ExecutablePath { get; private set; }
        public string? WorkingDirectory { get; private set; }
        public string? Arguments { get; private set; }
        public bool Start(string executablePath, string workingDirectory, string? arguments)
        {
            ExecutablePath = executablePath;
            WorkingDirectory = workingDirectory;
            Arguments = arguments;
            return true;
        }
    }

    private sealed class IdentityProcessLauncher : ILocalProcessLauncher, IProcessIdentityLauncher
    {
        public LaunchedProcessIdentity Identity { get; } = new(4812, DateTimeOffset.UtcNow);
        public bool Start(string executablePath, string workingDirectory, string? arguments) => true;
        public LaunchedProcessIdentity? StartWithIdentity(string executablePath, string workingDirectory, string? arguments) => Identity;
    }

    private sealed class RecordingSessionLaunchSink : IManualSessionLaunchSink
    {
        public Guid GameId { get; private set; }
        public LaunchedProcessIdentity? Identity { get; private set; }
        public void TrackLaunchedProcess(Guid gameId, int processId, DateTimeOffset startedAtUtc) =>
            (GameId, Identity) = (gameId, new LaunchedProcessIdentity(processId, startedAtUtc));
    }

    private sealed class RecordingUriLauncher : IExternalUriLauncher
    {
        public void Open(Uri uri) { }
    }
}
