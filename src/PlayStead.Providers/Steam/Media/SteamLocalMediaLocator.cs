using PlayStead.Core.Media;

namespace PlayStead.Providers.Steam.Media;

public sealed class SteamLocalMediaLocator
{
    public string? TryLocate(
        string steamRoot,
        string appId,
        GameMediaAssetType assetType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(steamRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        if (!appId.All(char.IsAsciiDigit))
        {
            return null;
        }

        string[] filenames = assetType switch
        {
            GameMediaAssetType.Cover =>
            [
                "library_capsule_2x.jpg",
                "library_capsule.jpg",
                "library_600x900_2x.jpg",
                "library_600x900.jpg"
            ],
            GameMediaAssetType.Header =>
            [
                "library_header.jpg",
                "header.jpg"
            ],
            GameMediaAssetType.Hero => ["library_hero.jpg"],
            GameMediaAssetType.Logo => ["logo.png"],
            _ => []
        };

        var libraryCacheRoot = Path.GetFullPath(
            Path.Combine(
                steamRoot,
                "appcache",
                "librarycache"));
        var appDirectory = Path.Combine(
            libraryCacheRoot,
            appId);

        foreach (var filename in filenames)
        {
            var candidate = TryGetRegularFile(
                libraryCacheRoot,
                Path.Combine(appDirectory, filename));

            if (candidate is not null)
            {
                return candidate;
            }
        }

        foreach (var filename in filenames)
        {
            var candidate = TryGetRegularFile(
                libraryCacheRoot,
                Path.Combine(
                    libraryCacheRoot,
                    $"{appId}_{filename}"));

            if (candidate is not null)
            {
                return candidate;
            }
        }

        var hashDirectories = GetHashDirectories(
            libraryCacheRoot,
            appDirectory);

        foreach (var filename in filenames)
        {
            foreach (var hashDirectory in hashDirectories)
            {
                var candidate = TryGetRegularFile(
                    appDirectory,
                    Path.Combine(hashDirectory, filename));

                if (candidate is not null)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetHashDirectories(
        string libraryCacheRoot,
        string appDirectory)
    {
        try
        {
            if (!IsWithinRoot(libraryCacheRoot, appDirectory) ||
                !Directory.Exists(appDirectory))
            {
                return [];
            }

            return Directory.GetDirectories(appDirectory)
                .Where(directory =>
                    IsHashDirectory(directory) &&
                    IsSafeDirectory(appDirectory, directory))
                .OrderBy(
                    Path.GetFileName,
                    StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            return [];
        }
    }

    private static bool IsHashDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return name.Length == 40 &&
               name.All(Uri.IsHexDigit);
    }

    private static bool IsSafeDirectory(
        string root,
        string candidate)
    {
        try
        {
            var attributes = File.GetAttributes(candidate);
            return IsWithinRoot(root, candidate) &&
                   (attributes & FileAttributes.Directory) != 0 &&
                   (attributes & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static string? TryGetRegularFile(
        string root,
        string candidate)
    {
        try
        {
            var fullPath = Path.GetFullPath(candidate);

            if (!IsWithinRoot(root, fullPath))
            {
                return null;
            }

            var attributes = File.GetAttributes(fullPath);
            return (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0
                ? fullPath
                : null;
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static bool IsWithinRoot(
        string root,
        string candidate)
    {
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var fullCandidate = Path.GetFullPath(candidate);

        return fullCandidate.StartsWith(
            fullRoot,
            StringComparison.OrdinalIgnoreCase);
    }
}
