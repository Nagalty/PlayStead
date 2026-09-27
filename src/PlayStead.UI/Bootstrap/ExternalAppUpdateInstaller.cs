using System.Diagnostics;
using System.IO;
using PlayStead.Core.Updates;

namespace PlayStead.UI.Bootstrap;

public sealed class ExternalAppUpdateInstaller : IAppUpdateInstaller
{
    private readonly AppUpdatePaths _paths;
    private int _started;

    public ExternalAppUpdateInstaller(AppUpdatePaths paths) => _paths = paths ?? throw new ArgumentNullException(nameof(paths));

    public bool TryStart(AppUpdateState state)
    {
        if (state.Status != AppUpdateStatus.ReadyToInstall || state.Channel != DistributionChannel.GitHub ||
            state.LocalPackagePath is not { Length: > 0 } package || state.AvailableVersion is not { Length: > 0 } version)
            return false;
        if (!File.Exists(package) || Interlocked.Exchange(ref _started, 1) != 0)
            return false;
        var targetDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var executable = Path.Combine(targetDirectory, "PlayStead.UI.exe");
        try
        {
            var runner = PrepareDetachedUpdater(package);
            if (runner is null)
            {
                Interlocked.Exchange(ref _started, 0);
                return false;
            }
            var updater = Path.Combine(runner, "PlayStead.Updater.exe");
            var arguments = $"--pid {Environment.ProcessId} --package {Quote(package)} --updates-root {Quote(_paths.UpdatesRoot)} --target-dir {Quote(targetDirectory)} --exe {Quote(executable)} --version {Quote(version)}";
            Trace.WriteLine($"[APP-UPDATE] DetachedUpdaterPrepared Path=\"{runner}\"");
            Trace.WriteLine($"[APP-UPDATE] InstallerLaunch PackagePath=\"{package}\" InstallDirectory=\"{targetDirectory}\" RelaunchPath=\"{executable}\" Version={version}");
            Trace.WriteLine($"[APP-UPDATE] DetachedUpdaterLaunch Path=\"{updater}\"");
            if (Process.Start(new ProcessStartInfo(updater, arguments) { UseShellExecute = true, WorkingDirectory = runner }) is null)
            {
                Interlocked.Exchange(ref _started, 0);
                return false;
            }
            System.Windows.Application.Current?.Shutdown();
            return true;
        }
        catch
        {
            Interlocked.Exchange(ref _started, 0);
            return false;
        }
    }

    private static string? PrepareDetachedUpdater(string package)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "UpdaterHost");
        if (!Directory.Exists(source))
            return null;

        var packageDirectory = Path.GetDirectoryName(Path.GetFullPath(package));
        if (string.IsNullOrWhiteSpace(packageDirectory))
            return null;
        var runner = Path.Combine(packageDirectory, "updater-runner");
        if (Directory.Exists(runner))
            Directory.Delete(runner, true);
        Directory.CreateDirectory(runner);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(runner, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
        return File.Exists(Path.Combine(runner, "PlayStead.Updater.exe")) ? runner : null;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
