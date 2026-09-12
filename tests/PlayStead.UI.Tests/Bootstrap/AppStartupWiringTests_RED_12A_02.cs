using System.Reflection;
using System.Windows;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class AppStartupWiringTests
{
    [Fact]
    public void App_declares_its_own_OnStartup_override()
    {
        var method =
            typeof(App).GetMethod(
                "OnStartup",
                BindingFlags.Instance
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);

        Assert.NotNull(method);

        Assert.Equal(
            typeof(void),
            method.ReturnType);

        Assert.Equal(
            [typeof(StartupEventArgs)],
            method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
    }

    [Fact]
    public void App_declares_its_own_OnExit_override()
    {
        var method =
            typeof(App).GetMethod(
                "OnExit",
                BindingFlags.Instance
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);

        Assert.NotNull(method);

        Assert.Equal(
            typeof(void),
            method.ReturnType);

        Assert.Equal(
            [typeof(ExitEventArgs)],
            method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
    }

    [Fact]
    public void App_owns_the_startup_coordinator_for_process_lifetime()
    {
        var fields =
            typeof(App).GetFields(
                BindingFlags.Instance
                | BindingFlags.NonPublic
                | BindingFlags.Public);

        Assert.Contains(
            fields,
            field =>
                field.FieldType ==
                typeof(ApplicationStartupCoordinator));
    }

    [Fact]
    public void App_code_behind_delegates_start_and_stop_to_the_coordinator()
    {
        var source =
            File.ReadAllText(
                FindRepositoryFile(
                    "src",
                    "PlayStead.UI",
                    "App.xaml.cs"));

        Assert.Contains(
            ".StartAsync(",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            ".StopAsync(",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "ApplicationStartupCoordinator",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void App_code_behind_remains_a_composition_root_not_database_or_scanner_implementation()
    {
        var source =
            File.ReadAllText(
                FindRepositoryFile(
                    "src",
                    "PlayStead.UI",
                    "App.xaml.cs"));

        var forbidden =
            new[]
            {
                "SqliteConnection",
                "DatabaseInitializer",
                "DatabaseHealthChecker",
                "LocalScanCoordinator",
                "ScanAllAsync(",
                "ApplySourceScanAsync(",
                "new MainWindow("
            };

        foreach (var token in forbidden)
        {
            Assert.DoesNotContain(
                token,
                source,
                StringComparison.Ordinal);
        }
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
