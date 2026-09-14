namespace PlayStead.Platform.Paths;

public sealed record UserDataLayout(
    string Root,
    string DatabasePath,
    string CacheDirectory,
    string SnapshotsDirectory,
    string RegistryDirectory,
    string LogsDirectory,
    string BackupsDirectory)
{
    public string MediaDirectory =>
        Path.Combine(Root, "Media");

    public static UserDataLayout FromRoot(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var fullRoot = Path.GetFullPath(root);

        return new UserDataLayout(
            fullRoot,
            Path.Combine(fullRoot, "Data", "playstead.db"),
            Path.Combine(fullRoot, "Cache"),
            Path.Combine(fullRoot, "Snapshots"),
            Path.Combine(fullRoot, "Registry"),
            Path.Combine(fullRoot, "Logs"),
            Path.Combine(fullRoot, "Backups"));
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(SnapshotsDirectory);
        Directory.CreateDirectory(RegistryDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(MediaDirectory);
    }
}
