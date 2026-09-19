using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class StartupTraceFileSinkTests
{
    [Fact]
    public void Installs_fresh_autoflushed_file_listener_and_is_failure_safe()
    {
        using var sink = new StartupTraceFileSink();
        Assert.False(sink.TryInstall("\0"));

        var root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));
        var expected = Path.Combine(root, "PlayStead", "Logs", "startup-trace.log");
        Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
        File.WriteAllText(expected, "stale run");

        try
        {
            Assert.True(sink.TryInstall(root));
            Assert.Equal(expected, sink.LogPath);
            Assert.True(System.Diagnostics.Trace.AutoFlush);
            System.Diagnostics.Trace.WriteLine("[STARTUP] test marker");
            System.Diagnostics.Trace.Flush();
            Assert.True(sink.TryInstall(root));
            sink.Dispose();
            Assert.Contains("[STARTUP] test marker", File.ReadAllText(expected));
            Assert.DoesNotContain("stale run", File.ReadAllText(expected));
        }
        finally
        {
            sink.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void App_installs_startup_trace_before_startup_coordinator_runs()
    {
        var appSource = FindRepositoryFile(Path.Combine("src", "PlayStead.UI", "App.xaml.cs"));
        var source = File.ReadAllText(appSource);
        var install = source.IndexOf("_startupTraceFileSink.TryInstall()", StringComparison.Ordinal);
        var coordinator = source.IndexOf("_startupCoordinator.StartAsync(", StringComparison.Ordinal);

        Assert.True(install >= 0 && coordinator > install);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, relativePath);
    }
}
