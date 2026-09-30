namespace PlayStead.Core.Persistence;

public static class ManualInstallRootHeuristics
{
    public static string Resolve(string executablePath, string workingDirectory, string? persistedRootPath)
    {
        if (!string.IsNullOrWhiteSpace(persistedRootPath) &&
            !string.Equals(persistedRootPath, workingDirectory, StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(persistedRootPath);

        var suggested = Suggest(executablePath);
        return string.Equals(suggested, workingDirectory, StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(workingDirectory)
            : suggested;
    }

    public static string Suggest(string executablePath)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(executablePath));
        if (string.IsNullOrWhiteSpace(parent))
            return string.Empty;

        var current = new DirectoryInfo(parent);
        if (current.Name.Equals("Retail", StringComparison.OrdinalIgnoreCase) && current.Parent is not null)
            return current.Parent.FullName;

        if (current.Name.Equals("Win64", StringComparison.OrdinalIgnoreCase) &&
            current.Parent?.Name.Equals("Binaries", StringComparison.OrdinalIgnoreCase) == true &&
            current.Parent.Parent is not null)
            return current.Parent.Parent.FullName;

        if (current.Name.Equals("Shipping", StringComparison.OrdinalIgnoreCase) &&
            current.Parent?.Name.Equals("Win64", StringComparison.OrdinalIgnoreCase) == true &&
            current.Parent.Parent?.Name.Equals("Binaries", StringComparison.OrdinalIgnoreCase) == true &&
            current.Parent.Parent.Parent is not null)
            return current.Parent.Parent.Parent.FullName;

        return current.FullName;
    }
}
