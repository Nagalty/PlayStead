namespace PlayStead.Core.LocalEnvironment;

public sealed record LocalEnvironmentSaveScanPolicy(int MaxFiles = 4096, int MaxDepth = 2, long MaxTotalSizeBytes = 256L * 1024 * 1024);

public sealed record LocalEnvironmentSaveScanResult(
    bool Exists,
    DateTimeOffset? LastModifiedUtc,
    IReadOnlyDictionary<string, string> Metadata);

public static class LocalEnvironmentSaveScanner
{
    public static LocalEnvironmentSaveScanResult Scan(
        string path,
        LocalEnvironmentPathKind pathKind,
        CancellationToken cancellationToken,
        LocalEnvironmentSaveScanPolicy? policy = null)
    {
        policy ??= new LocalEnvironmentSaveScanPolicy();
        if (pathKind == LocalEnvironmentPathKind.File)
        {
            if (!File.Exists(path)) return Missing();
            var info = new FileInfo(path);
            return Result(true, info.LastWriteTimeUtc, 1, info.Length, false);
        }

        if (pathKind != LocalEnvironmentPathKind.Directory || !Directory.Exists(path))
            return Missing();

        var files = 0;
        long size = 0;
        DateTimeOffset? newest = null;
        var root = new DirectoryInfo(path);
        var queue = new Queue<(DirectoryInfo Directory, int Depth)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0 && files < policy.MaxFiles && size <= policy.MaxTotalSizeBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directory, depth) = queue.Dequeue();
            foreach (var file in directory.EnumerateFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (files >= policy.MaxFiles || size > policy.MaxTotalSizeBytes) break;
                files++;
                size = checked(size + file.Length);
                var fileModified = file.LastWriteTimeUtc;
                if (newest is null || fileModified > newest) newest = fileModified;
            }

            if (depth < policy.MaxDepth)
            {
                foreach (var child in directory.EnumerateDirectories())
                {
                    if (files >= policy.MaxFiles || size > policy.MaxTotalSizeBytes) break;
                    queue.Enqueue((child, depth + 1));
                }
            }
        }

        var modified = newest ?? root.LastWriteTimeUtc;
        return Result(true, modified, files, size, true);
    }

    private static LocalEnvironmentSaveScanResult Missing() => new(false, null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private static LocalEnvironmentSaveScanResult Result(bool exists, DateTimeOffset modified, int files, long size, bool directory) =>
        new(exists, modified, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FileCount"] = files.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["TotalSizeBytes"] = size.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["IsDirectory"] = directory ? "true" : "false",
            ["CloudProvider"] = LocalEnvironmentCloudProvider.Unknown.ToString()
        });
}
