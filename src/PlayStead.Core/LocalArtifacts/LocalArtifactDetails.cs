namespace PlayStead.Core.LocalArtifacts;

/// <summary>Bounded metadata for an already known local artifact.</summary>
public sealed record LocalArtifactDetails(
    bool IsFile,
    int? FileCount,
    long? TotalBytes,
    DateTimeOffset? LastModifiedUtc)
{
    public static LocalArtifactDetails Unavailable(bool isFile) =>
        new(isFile, null, null, null);
}

public static class LocalArtifactDetailsReader
{
    public static LocalArtifactDetails? Read(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                return new LocalArtifactDetails(
                    true,
                    1,
                    info.Length,
                    info.LastWriteTimeUtc == DateTime.MinValue
                        ? null
                        : new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
            }

            if (!Directory.Exists(path))
                return null;

            var count = 0;
            long totalBytes = 0;
            DateTimeOffset? latest = null;
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var filePath in Directory.EnumerateFiles(path, "*", options))
            {
                try
                {
                    var info = new FileInfo(filePath);
                    count++;
                    totalBytes = checked(totalBytes + info.Length);
                    var modified = info.LastWriteTimeUtc;
                    if (modified != DateTime.MinValue)
                    {
                        var timestamp = new DateTimeOffset(modified, TimeSpan.Zero);
                        if (latest is null || timestamp > latest) latest = timestamp;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            return new LocalArtifactDetails(false, count, totalBytes, latest);
        }
        catch (IOException)
        {
            return File.Exists(path)
                ? LocalArtifactDetails.Unavailable(true)
                : Directory.Exists(path)
                    ? LocalArtifactDetails.Unavailable(false)
                    : null;
        }
        catch (UnauthorizedAccessException)
        {
            return File.Exists(path)
                ? LocalArtifactDetails.Unavailable(true)
                : Directory.Exists(path)
                    ? LocalArtifactDetails.Unavailable(false)
                    : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
