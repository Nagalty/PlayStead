using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamLibraryFoldersReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Read_includes_main_root_and_distinct_existing_secondary_libraries()
    {
        var steamRoot = Path.Combine(_root, "Steam");
        var secondaryA = Path.Combine(_root, "Library A");
        var secondaryB = Path.Combine(_root, "Library B");

        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        Directory.CreateDirectory(Path.Combine(secondaryA, "steamapps"));
        Directory.CreateDirectory(Path.Combine(secondaryB, "steamapps"));

        var fixture = File.ReadAllText(FixturePath("libraryfolders.vdf"))
            .Replace("__MAIN__", EscapeValvePath(steamRoot), StringComparison.Ordinal)
            .Replace("__SECONDARY_A__", EscapeValvePath(secondaryA), StringComparison.Ordinal)
            .Replace("__SECONDARY_B__", EscapeValvePath(secondaryB), StringComparison.Ordinal);

        File.WriteAllText(
            Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
            fixture);

        var roots = new SteamLibraryFoldersReader().Read(steamRoot);

        Assert.Equal(3, roots.Count);
        Assert.Contains(Path.GetFullPath(steamRoot), roots, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.GetFullPath(secondaryA), roots, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.GetFullPath(secondaryB), roots, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            roots.Count,
            roots.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Read_returns_main_root_when_libraryfolders_file_is_missing()
    {
        var steamRoot = Path.Combine(_root, "Steam");
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));

        var roots = new SteamLibraryFoldersReader().Read(steamRoot);

        Assert.Single(roots);
        Assert.Equal(
            Path.GetFullPath(steamRoot),
            roots[0],
            StringComparer.OrdinalIgnoreCase);
    }

    private static string FixturePath(string name) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Steam",
            name);

    private static string EscapeValvePath(string path) =>
        path.Replace(@"\", @"\\", StringComparison.Ordinal);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
