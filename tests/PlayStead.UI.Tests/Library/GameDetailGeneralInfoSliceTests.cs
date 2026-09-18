using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailGeneralInfoSliceTests
{
    [Fact]
    public void General_info_card_is_not_created_without_reliable_projected_metadata()
    {
        var path = Path.Combine(FindRoot(), "src", "PlayStead.UI", "Library", "GameDetailView.xaml");
        var xaml = File.ReadAllText(path);

        Assert.DoesNotContain("Infos générales", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Developer", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Publisher", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ReleaseDate", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Genre", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Langue", xaml, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PlayStead.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}