namespace PlayStead.UI.Tests.Bootstrap;

public sealed class RefreshAndApplyUiStageTests
{
    [Fact]
    public void Final_ui_apply_does_not_reload_library_projection()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "PlayStead.UI", "App.xaml.cs"));
        var start = source.IndexOf("ApplySnapshotOnUiAsync:", StringComparison.Ordinal);
        var end = source.IndexOf("StopPipeAsync:", start, StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);

        var block = source[start..end];
        Assert.DoesNotContain("LibraryViewModel", block, StringComparison.Ordinal);
        Assert.Contains("SessionViewModel", block, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] relativeParts)
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var candidate = Path.Combine([current.FullName, .. relativeParts]);
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, relativeParts));
    }
}
