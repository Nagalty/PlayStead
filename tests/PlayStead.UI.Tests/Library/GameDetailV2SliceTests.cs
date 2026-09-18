using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailV2SliceTests
{
    private static string View => File.ReadAllText(Path.Combine(FindRoot(), "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));

    [Fact]
    public void TonJeu_UsesSharedCardAndReliableSessionProjection()
    {
        var xaml = View;
        Assert.Contains("x:Name=\"GameActivity\"", xaml);
        Assert.Contains("Title=\"Ton jeu\"", xaml);
        Assert.Contains("Style=\"{DynamicResource PlayStead.Surface.Card}\"", xaml);
        Assert.Contains("PlayStead.Surface.Metric", xaml);
        Assert.Contains("Activity.TotalPlayTimeLabel", xaml);
        Assert.Contains("Activity.LastSessionDateLabel", xaml);
        Assert.Contains("Activity.SessionCountLabel", xaml);
    }

    [Fact]
    public void Installation_ExposesOnlyAuthoritativeLocalFields()
    {
        var xaml = View;
        Assert.Contains("Title=\"Installation\"", xaml);
        Assert.Contains("InstalledSizeLabel", xaml);
        Assert.Contains("InstallDriveLabel", xaml);
        Assert.Contains("InstallPath", xaml);
        Assert.DoesNotContain("Données disponibles", xaml);
        Assert.DoesNotContain("État local", xaml);
        Assert.DoesNotContain("Title=\"Source / plateforme\"", xaml);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PlayStead.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
