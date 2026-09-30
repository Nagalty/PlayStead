namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailGraphicsTechnologyCopyTests
{
    [Fact]
    public void Graphics_section_uses_availability_copy_without_technical_details()
    {
        var xaml = File.ReadAllText(Path.Combine(FindRoot(), "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));

        Assert.Contains("Technologies graphiques", xaml);
        Assert.Contains("RuntimeStatusLabel", xaml);
        Assert.DoesNotContain("RuntimeVersion", xaml);
        Assert.DoesNotContain("ActivationStatusLabel", xaml);
        Assert.DoesNotContain("Runtime présent", xaml);
        Assert.DoesNotContain("Activation inconnue", xaml);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PlayStead.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
