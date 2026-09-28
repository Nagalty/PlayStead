namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailLocalArtifactsStructureTests
{
    [Fact]
    public void Game_detail_has_conditional_local_artifact_section_and_safe_open_action()
    {
        var path = Path.Combine(FindRoot(), "src", "PlayStead.UI", "Library", "GameDetailView.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("x:Name=\"LocalArtifactsSection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding HasLocalArtifacts", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding LocalArtifacts}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding KindLabel}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Path}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.OpenLocalArtifactFolderCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding Exists}\"", xaml, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
