namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailModificationsSectionTests
{
    [Fact]
    public void Modifications_section_is_separate_from_general_info()
    {
        var xaml = File.ReadAllText(Path.Combine(FindRoot(), "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));

        Assert.Contains("<controls:SectionHeader Title=\"Mods\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding HasModEvidence", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Modifications\" Style=\"{DynamicResource PlayStead.Text.Caption}", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding ModStatusLabel}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"5\"", xaml, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
