namespace PlayStead.UI.Tests.Bootstrap;

public sealed class Task09SessionStartupOrderingTests
{
    [Fact]
    public void Coordinator_builds_host_then_initializes_local_state_then_starts_hosted_services()
    {
        var source = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "PlayStead.UI",
                "Bootstrap",
                "ApplicationStartupCoordinator.cs"));

        var buildIndex = source.IndexOf(
            "_operations.BuildHostAsync(",
            StringComparison.Ordinal);

        var initializeIndex = source.IndexOf(
            "_operations.InitializeLocalStateAsync(",
            StringComparison.Ordinal);

        var startIndex = source.IndexOf(
            "_operations.StartHostAsync(",
            StringComparison.Ordinal);

        Assert.True(
            buildIndex >= 0,
            "Coordinator must explicitly build the host before local-state initialization.");

        Assert.True(
            initializeIndex > buildIndex,
            "Local state must initialize after the DI host is built.");

        Assert.True(
            startIndex > initializeIndex,
            "Hosted services must start only after database/local-state initialization succeeds.");
    }

    [Fact]
    public void App_separates_host_build_from_host_start()
    {
        var source = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "PlayStead.UI",
                "App.xaml.cs"));

        Assert.Contains(
            "BuildHostAsync",
            source,
            StringComparison.Ordinal);

        var buildMember = source.IndexOf(
            "BuildHostAsync",
            StringComparison.Ordinal);

        var startMember = source.IndexOf(
            "StartHostAsync",
            buildMember + 1,
            StringComparison.Ordinal);

        Assert.True(
            buildMember >= 0 && startMember > buildMember,
            "App must expose distinct build and start host operations.");

        Assert.Contains(
            "PlaySteadHost.Build(",
            source,
            StringComparison.Ordinal);

        Assert.Matches(
            @"await\s+\w+\.StartAsync\(",
            source);
    }

    private static string FindRepositoryFile(
        params string[] relativeParts)
    {
        var starts = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var start in starts)
        {
            var current = new DirectoryInfo(
                Path.GetFullPath(start));

            while (current is not null)
            {
                var candidateParts =
                    new string[relativeParts.Length + 1];

                candidateParts[0] = current.FullName;

                Array.Copy(
                    relativeParts,
                    0,
                    candidateParts,
                    1,
                    relativeParts.Length);

                var candidate = Path.Combine(candidateParts);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {Path.Combine(relativeParts)}");
    }
}
