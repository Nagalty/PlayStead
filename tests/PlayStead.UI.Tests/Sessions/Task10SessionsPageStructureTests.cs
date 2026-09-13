using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace PlayStead.UI.Tests.Sessions;

public sealed class Task10SessionsPageStructureTests
{
    [Fact]
    public void SessionsView_exposes_the_structured_sessions_shell()
    {
        var viewType =
            typeof(MainWindow)
                .Assembly
                .GetType(
                    "PlayStead.UI.Sessions.SessionsView");

        Assert.True(
            viewType is not null,
            "Task 10 requires a real PlayStead.UI.Sessions.SessionsView.");

        RunSta(() =>
        {
            var view = Assert.IsAssignableFrom<FrameworkElement>(
                Activator.CreateInstance(
                    viewType!));

            var heading = Assert.IsType<TextBlock>(
                view.FindName("SessionsHeading"));

            var activeHeading = Assert.IsType<TextBlock>(
                view.FindName("ActiveSessionsHeading"));

            var activeList = Assert.IsType<ItemsControl>(
                view.FindName("ActiveSessionsList"));

            var activeEmpty = Assert.IsAssignableFrom<FrameworkElement>(
                view.FindName("ActiveSessionsEmptyState"));

            var recentHeading = Assert.IsType<TextBlock>(
                view.FindName("RecentHistoryHeading"));

            var recentHost = Assert.IsAssignableFrom<FrameworkElement>(
                view.FindName("RecentHistoryHost"));

            Assert.Equal(
                "Sessions",
                heading.Text);

            Assert.Equal(
                "Sessions en cours",
                activeHeading.Text);

            Assert.Equal(
                "Historique récent",
                recentHeading.Text);

            Assert.NotNull(activeList);
            Assert.NotNull(activeEmpty);
            Assert.NotNull(recentHost);

            return 0;
        });
    }

    [Fact]
    public void SessionsView_binds_active_session_identity_start_and_live_duration_without_implementing_task11_detail()
    {
        var xamlPath =
            FindRepositoryFile(
                "src",
                "PlayStead.UI",
                "Sessions",
                "SessionsView.xaml");

        Assert.True(
            File.Exists(xamlPath),
            "Task 10 requires SessionsView.xaml.");

        var xaml =
            File.ReadAllText(
                xamlPath);

        Assert.Contains(
            "ItemsSource=\"{Binding ActiveSessions}\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "x:Name=\"ActiveSessionsEmptyState\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "x:Name=\"RecentHistoryHost\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Matches(
            @"\{Binding\s+Title\b",
            xaml);

        Assert.Matches(
            @"\{Binding\s+StartedAtLabel\b",
            xaml);

        Assert.Matches(
            @"\{Binding\s+DurationLabel\b",
            xaml);
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

        return Path.Combine(
            relativeParts);
    }

    private static T RunSta<T>(
        Func<T> action)
    {
        T? result = default;
        Exception? error = null;

        var thread = new Thread(
            () =>
            {
                try
                {
                    result = action();
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            });

        thread.SetApartmentState(
            ApartmentState.STA);

        thread.Start();
        thread.Join();

        if (error is not null)
        {
            ExceptionDispatchInfo
                .Capture(error)
                .Throw();
        }

        return result!;
    }
}
