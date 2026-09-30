namespace PlayStead.Core.Persistence;

public static class ManualInstallSizeCalculator
{
    public static Task<long?> TryCalculateAsync(
        string installRootPath,
        CancellationToken cancellationToken) =>
        Task.Run(() => TryCalculate(installRootPath, cancellationToken), cancellationToken);

    private static long? TryCalculate(string installRootPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(installRootPath) || !Directory.Exists(installRootPath))
            return null;

        var total = 0L;
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(installRootPath));

        try
        {
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = pending.Pop();
                var info = new DirectoryInfo(directory);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    continue;

                foreach (var filePath in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        continue;
                    checked { total += fileInfo.Length; }
                }

                foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                    pending.Push(child);
            }

            return total;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
