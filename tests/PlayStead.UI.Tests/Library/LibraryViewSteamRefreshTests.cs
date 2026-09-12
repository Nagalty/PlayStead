namespace PlayStead.UI.Tests.Library;

public sealed class LibraryViewSteamRefreshTests
{
    [Fact]
    public void Library_view_exposes_global_Steam_verification_button_before_game_list()
    {
        var xaml = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "PlayStead.UI",
                "Library",
                "LibraryView.xaml"));

        Assert.Contains(
            "Vérifier Steam",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "x:Name=\"VerifySteamButton\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "IsEnabled=\"{Binding CanVerifySteam}\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "Click=\"VerifySteamButton_OnClick\"",
            xaml,
            StringComparison.Ordinal);

        var buttonIndex =
            xaml.IndexOf(
                "x:Name=\"VerifySteamButton\"",
                StringComparison.Ordinal);

        var gameListIndex =
            xaml.IndexOf(
                "x:Name=\"GameList\"",
                StringComparison.Ordinal);

        Assert.True(
            buttonIndex >= 0 &&
            gameListIndex >= 0 &&
            buttonIndex < gameListIndex,
            "The global Steam verification action must remain in the library header, before the game list.");
    }

    [Fact]
    public void Library_view_renders_Steam_status_label_from_each_item()
    {
        var xaml = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "PlayStead.UI",
                "Library",
                "LibraryView.xaml"));

        Assert.Contains(
            "SteamStatusLabel",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "HasSteamStatus",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Library_view_code_behind_delegates_manual_verification_to_view_model()
    {
        var codeBehind = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "PlayStead.UI",
                "Library",
                "LibraryView.xaml.cs"));

        Assert.Contains(
            "VerifySteamButton_OnClick",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "VerifySteamAsync(",
            codeBehind,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "SteamCmd",
            codeBehind,
            StringComparison.OrdinalIgnoreCase);
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
