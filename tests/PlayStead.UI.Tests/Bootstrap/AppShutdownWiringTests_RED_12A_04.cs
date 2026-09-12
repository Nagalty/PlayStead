namespace PlayStead.UI.Tests.Bootstrap;

public sealed class AppShutdownWiringTests
{
    [Fact]
    public void OnExit_moves_async_coordinator_shutdown_off_the_WPF_dispatcher_thread()
    {
        var source =
            File.ReadAllText(
                FindRepositoryFile(
                    "src",
                    "PlayStead.UI",
                    "App.xaml.cs"));

        var onExitStart =
            source.IndexOf(
                "protected override void OnExit",
                StringComparison.Ordinal);

        Assert.True(
            onExitStart >= 0,
            "App.xaml.cs must declare OnExit.");

        var nextMember =
            source.IndexOf(
                "private ApplicationStartupCoordinator.Operations",
                onExitStart,
                StringComparison.Ordinal);

        Assert.True(
            nextMember > onExitStart,
            "Could not isolate the OnExit implementation.");

        var onExitSource =
            source[
                onExitStart..
                nextMember];

        Assert.Contains(
            ".StopAsync(",
            onExitSource,
            StringComparison.Ordinal);

        Assert.Contains(
            "Task.Run(",
            onExitSource,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(
        params string[] relativeParts)
    {
        var starts =
            new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory
            };

        foreach (var start in starts)
        {
            var current =
                new DirectoryInfo(
                    Path.GetFullPath(start));

            while (current is not null)
            {
                var candidateParts =
                    new string[
                        relativeParts.Length + 1];

                candidateParts[0] =
                    current.FullName;

                Array.Copy(
                    relativeParts,
                    0,
                    candidateParts,
                    1,
                    relativeParts.Length);

                var candidate =
                    Path.Combine(
                        candidateParts);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current =
                    current.Parent;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {Path.Combine(relativeParts)}");
    }
}
