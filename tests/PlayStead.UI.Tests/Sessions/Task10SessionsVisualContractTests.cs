using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class Task10SessionsVisualContractTests
{
    [Fact]
    public void SessionsView_prioritizes_active_sessions_before_history_and_uses_reusable_section_headers()
    {
        var xaml =
            File.ReadAllText(
                FindRepositoryFile(
                    "src",
                    "PlayStead.UI",
                    "Sessions",
                    "SessionsView.xaml"));

        var activeIndex =
            xaml.IndexOf(
                "x:Name=\"ActiveSessionsHeading\"",
                StringComparison.Ordinal);

        var historyIndex =
            xaml.IndexOf(
                "x:Name=\"RecentHistoryHeading\"",
                StringComparison.Ordinal);

        Assert.True(
            activeIndex >= 0,
            "SessionsView must keep an explicit active-session section.");

        Assert.True(
            historyIndex >= 0,
            "SessionsView must keep an explicit recent-history section.");

        Assert.True(
            activeIndex < historyIndex,
            "The active-session section must appear before recent history.");

        Assert.True(
            CountOccurrences(
                xaml,
                "<controls:SectionHeader") >= 2,
            "Task 10 requires the Sessions page to use the reusable 0.4 SectionHeader control for its active and history sections.");
    }

    [Fact]
    public void SessionDetailView_exposes_textual_observed_effective_corrected_and_recovered_meaning()
    {
        var xaml =
            File.ReadAllText(
                FindRepositoryFile(
                    "src",
                    "PlayStead.UI",
                    "Sessions",
                    "SessionDetailView.xaml"));

        var visibleLiterals =
            ExtractVisibleLiteralText(
                xaml);

        Assert.Contains(
            visibleLiterals,
            value =>
                value.Contains(
                    "Observ",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            visibleLiterals,
            value =>
                value.Contains(
                    "Effect",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            visibleLiterals,
            value =>
                value.Contains(
                    "Corrigé",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Contains(
                    "Corrigée",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            visibleLiterals,
            value =>
                value.Contains(
                    "Récupér",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SessionDetailView_keeps_existing_detail_and_correction_controls_reachable()
    {
        RunSta(
            () =>
            {
                var view =
                    new SessionDetailView();

                foreach (var textName in new[]
                {
                    "DetailTitle",
                    "ObservedStartText",
                    "ObservedEndText",
                    "ObservedDurationText",
                    "EffectiveStartText",
                    "EffectiveEndText",
                    "EffectiveDurationText",
                    "EndReasonText",
                    "CorrectionValidationText"
                })
                {
                    Assert.IsType<TextBlock>(
                        view.FindName(
                            textName));
                }

                Assert.IsAssignableFrom<FrameworkElement>(
                    view.FindName(
                        "RecoveryBadge"));

                Assert.IsAssignableFrom<FrameworkElement>(
                    view.FindName(
                        "CorrectionBadge"));

                Assert.IsAssignableFrom<FrameworkElement>(
                    view.FindName(
                        "CorrectionPanel"));

                Assert.IsType<Button>(
                    view.FindName(
                        "CorrectSessionButton"));

                Assert.IsType<Button>(
                    view.FindName(
                        "SaveCorrectionButton"));
            });
    }

    private static IReadOnlyList<string> ExtractVisibleLiteralText(
        string xaml)
    {
        var values =
            new List<string>();

        foreach (var marker in new[]
        {
            "Text=\"",
            "Content=\"",
            "Message=\""
        })
        {
            var searchIndex = 0;

            while (searchIndex < xaml.Length)
            {
                var start =
                    xaml.IndexOf(
                        marker,
                        searchIndex,
                        StringComparison.Ordinal);

                if (start < 0)
                {
                    break;
                }

                start += marker.Length;

                var end =
                    xaml.IndexOf(
                        '"',
                        start);

                if (end < 0)
                {
                    break;
                }

                var value =
                    xaml[start..end];

                if (!value.StartsWith(
                        "{Binding",
                        StringComparison.Ordinal))
                {
                    values.Add(
                        value);
                }

                searchIndex =
                    end + 1;
            }
        }

        return values;
    }

    private static int CountOccurrences(
        string value,
        string token)
    {
        var count = 0;
        var index = 0;

        while (index < value.Length)
        {
            index =
                value.IndexOf(
                    token,
                    index,
                    StringComparison.Ordinal);

            if (index < 0)
            {
                break;
            }

            count++;
            index += token.Length;
        }

        return count;
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
                    Path.GetFullPath(
                        start));

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

                if (File.Exists(
                        candidate))
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

    private static void RunSta(
        Action action)
    {
        Exception? failure =
            null;

        var thread =
            new Thread(
                () =>
                {
                    try
                    {
                        action();
                    }
                    catch (
                        Exception exception)
                    {
                        failure =
                            exception;
                    }
                });

        thread.SetApartmentState(
            ApartmentState.STA);

        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo
                .Capture(
                    failure)
                .Throw();
        }
    }
}
