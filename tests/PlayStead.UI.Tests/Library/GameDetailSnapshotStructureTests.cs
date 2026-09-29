namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailSnapshotStructureTests
{
    [Fact]
    public void Game_detail_exposes_snapshot_actions_with_restore()
    {
        var path = Path.Combine(FindRoot(), "src", "PlayStead.UI", "Library", "GameDetailView.xaml");
        var xaml = File.ReadAllText(path);
        Assert.Contains("Créer une sauvegarde", xaml, StringComparison.Ordinal);
        Assert.Contains("CreateLocalArtifactSnapshotCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("DeleteLocalArtifactSnapshotCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("RestoreLocalArtifactSnapshotCommand", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Game_detail_exposes_local_artifact_metadata_and_conditional_opening()
    {
        var path = Path.Combine(FindRoot(), "src", "PlayStead.UI", "Library", "GameDetailView.xaml");
        var xaml = File.ReadAllText(path);
        Assert.Contains("{Binding DetailsLabel}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding OpenActionLabel}", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding HasDetails, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("OpenLocalArtifactFolderCommand", xaml, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
