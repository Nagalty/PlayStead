using PlayStead.Providers.Steam.ValveText;

namespace PlayStead.Providers.Steam;

public sealed class SteamLibraryFoldersReader
{
    public IReadOnlyList<string> Read(string steamRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(steamRoot);

        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIfValid(roots, steamRoot);

        var file = Path.Combine(
            steamRoot,
            "steamapps",
            "libraryfolders.vdf");

        if (!File.Exists(file))
        {
            return roots
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var parsed = ValveTextParser.Parse(File.ReadAllText(file));

        if (!parsed.TryGetValue("libraryfolders", out var node) ||
            node is not IReadOnlyDictionary<string, object> folders)
        {
            return roots
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        foreach (var child in folders.Values
                     .OfType<IReadOnlyDictionary<string, object>>())
        {
            if (child.TryGetValue("path", out var pathValue) &&
                pathValue is string path)
            {
                AddIfValid(roots, path);
            }
        }

        return roots
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddIfValid(
        HashSet<string> roots,
        string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (Directory.Exists(Path.Combine(fullPath, "steamapps")))
        {
            roots.Add(fullPath);
        }
    }
}
