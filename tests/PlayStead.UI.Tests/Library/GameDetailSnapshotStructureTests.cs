namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailSnapshotStructureTests
{
    [Fact]
    public void Game_detail_exposes_snapshot_actions_without_restore()
    {
        var path = Path.Combine(FindRoot(), "src", "PlayStead.UI", "Library", "GameDetailView.xaml");
        var xaml = File.ReadAllText(path);
        Assert.Contains("Créer un snapshot", xaml, StringComparison.Ordinal);
        Assert.Contains("CreateLocalArtifactSnapshotCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("DeleteLocalArtifactSnapshotCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Restaurer", xaml, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
