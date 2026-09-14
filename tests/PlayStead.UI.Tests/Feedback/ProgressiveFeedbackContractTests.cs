using System.Reflection;
using System.Xml.Linq;

namespace PlayStead.UI.Tests.Feedback;

public sealed class ProgressiveFeedbackContractTests
{
    [Fact]
    public void Library_keeps_local_content_visible_during_secondary_Steam_enrichment()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryViewModel.cs")));

        var checkingAssignment =
            source.IndexOf(
                "IsSteamChecking = true",
                StringComparison.Ordinal);

        var remoteRefresh =
            source.IndexOf(
                "await runtime.RefreshAllAsync(",
                StringComparison.Ordinal);

        Assert.True(
            checkingAssignment >= 0,
            "Steam enrichment must expose its secondary loading state.");

        Assert.True(
            remoteRefresh > checkingAssignment,
            "Local Library content must enter its secondary checking state before awaiting Steam enrichment.");

        Assert.DoesNotContain(
            "Items = Array.Empty",
            source,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Items = []",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Library_does_not_use_a_full_page_spinner_for_secondary_data()
    {
        var xaml =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        var fullPageProgress =
            xaml
                .Descendants()
                .Where(
                    element =>
                        element.Name.LocalName is
                            "ProgressBar" or
                            "ProgressRing")
                .ToArray();

        Assert.Empty(
            fullPageProgress);
    }

    [Fact]
    public void Secondary_Steam_failure_has_local_actionable_feedback_contract()
    {
        var viewModelType =
            typeof(MainWindow)
                .Assembly
                .GetType(
                    "PlayStead.UI.Library.LibraryViewModel",
                    throwOnError: false,
                    ignoreCase: false);

        Assert.NotNull(
            viewModelType);

        var errorProperty =
            viewModelType!.GetProperty(
                "SteamVerificationError",
                BindingFlags.Public |
                BindingFlags.Instance);

        Assert.True(
            errorProperty is not null &&
            errorProperty.PropertyType == typeof(string),
            "LibraryViewModel must expose a local SteamVerificationError string for secondary failures.");

        var hasErrorProperty =
            viewModelType.GetProperty(
                "HasSteamVerificationError",
                BindingFlags.Public |
                BindingFlags.Instance);

        Assert.True(
            hasErrorProperty is not null &&
            hasErrorProperty.PropertyType == typeof(bool),
            "LibraryViewModel must expose HasSteamVerificationError so only the Steam verification section reports the failure.");

        var xaml =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        var errorText =
            FindRequiredByName(
                xaml,
                "SteamVerificationErrorText");

        Assert.Equal(
            "{Binding SteamVerificationError}",
            errorText.Attribute(
                "Text")?.Value);

        Assert.Contains(
            "Vérifier Steam",
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml"))),
            StringComparison.Ordinal);
    }

    private static XElement FindRequiredByName(
        XDocument document,
        string name)
    {
        return document
                   .Descendants()
                   .SingleOrDefault(
                       element =>
                           element
                               .Attributes()
                               .Any(
                                   attribute =>
                                       attribute.Name.LocalName ==
                                           "Name" &&
                                       attribute.Value ==
                                           name))
               ?? throw new Xunit.Sdk.XunitException(
                   $"Element with x:Name '{name}' was not found.");
    }

    private static string FindUiFile(
        string relativePath)
    {
        var directory =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (directory is not null)
        {
            var uiDirectory =
                Path.Combine(
                    directory.FullName,
                    "src",
                    "PlayStead.UI");

            if (Directory.Exists(
                    uiDirectory))
            {
                var path =
                    Path.Combine(
                        uiDirectory,
                        relativePath);

                Assert.True(
                    File.Exists(
                        path),
                    $"Required UI file missing: {relativePath}");

                return path;
            }

            directory =
                directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "PlayStead.UI source directory was not found.");
    }
}
