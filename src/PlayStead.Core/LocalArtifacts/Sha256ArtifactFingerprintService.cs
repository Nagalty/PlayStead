using System.Security.Cryptography;
using System.Text;

namespace PlayStead.Core.LocalArtifacts;

public sealed class Sha256ArtifactFingerprintService : IArtifactFingerprintService
{
    public async Task<ArtifactFingerprintResult> ComputeAsync(
        GameLocalArtifact artifact,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeRoot(artifact.Path, out var root))
            return ArtifactFingerprintResult.Unavailable("Invalid artifact path.");

        try
        {
            var attributes = File.GetAttributes(root);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return ArtifactFingerprintResult.Unavailable("Reparse point artifact root is not supported.");

            if (File.Exists(root))
            {
                var info = new FileInfo(root);
                var hash = await HashFileAsync(root, cancellationToken).ConfigureAwait(false);
                return new ArtifactFingerprintResult(
                    new ArtifactFingerprint("SHA256", hash, 1, info.Length, DateTimeOffset.UtcNow),
                    null);
            }

            if (!Directory.Exists(root))
                return ArtifactFingerprintResult.Unavailable("Artifact is missing.");

            var entries = new List<string>();
            var totalSize = 0L;
            var files = 0;
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = pending.Pop();
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entryAttributes = File.GetAttributes(entry);
                    if ((entryAttributes & FileAttributes.ReparsePoint) != 0)
                        return ArtifactFingerprintResult.Unavailable("Reparse point inside artifact root is not supported.");

                    if (Directory.Exists(entry))
                    {
                        pending.Push(entry);
                        continue;
                    }

                    if (!File.Exists(entry) || !IsUnderRoot(root, entry))
                        return ArtifactFingerprintResult.Unavailable("Artifact entry escaped its root.");

                    var info = new FileInfo(entry);
                    var relative = NormalizeRelativePath(root, entry);
                    var hash = await HashFileAsync(entry, cancellationToken).ConfigureAwait(false);
                    entries.Add($"{relative}\n{info.Length}\n{hash}");
                    totalSize += info.Length;
                    files++;
                }
            }

            entries.Sort(StringComparer.Ordinal);
            var manifest = string.Join("\n", entries);
            var finalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest)));
            return new ArtifactFingerprintResult(
                new ArtifactFingerprint("SHA256", finalHash, files, totalSize, DateTimeOffset.UtcNow),
                null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return ArtifactFingerprintResult.Unavailable(exception.GetType().Name);
        }
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static bool TryNormalizeRoot(string path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            return false;
        try
        {
            normalized = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return Path.IsPathFullyQualified(normalized);
        }
        catch (ArgumentException) { return false; }
    }

    private static bool IsUnderRoot(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string NormalizeRelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
}
