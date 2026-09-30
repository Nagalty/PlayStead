using System.Text;

namespace PlayStead.Core.LocalArtifacts;

public enum LocalArtifactComparisonStatus
{
    Available,
    Identical,
    ReferenceUnavailable,
    CurrentUnavailable,
    UnsupportedFormat,
    TooLarge,
    Unavailable
}

public sealed record LocalArtifactDiffLine(int LineNumber, string Text, bool IsRemoved, bool IsAdded);

public sealed record LocalArtifactDiffHunk(int StartLine, IReadOnlyList<LocalArtifactDiffLine> Lines);

public sealed record LocalArtifactComparison(
    LocalArtifactComparisonStatus Status,
    int AddedLineCount = 0,
    int RemovedLineCount = 0,
    int ChangedLineCount = 0,
    IReadOnlyList<LocalArtifactDiffHunk>? Hunks = null)
{
    public IReadOnlyList<LocalArtifactDiffHunk> DiffHunks => Hunks ?? [];
}

public interface ILocalArtifactComparisonService
{
    Task<LocalArtifactComparison> CompareAsync(string referencePath, string currentPath, CancellationToken cancellationToken);
}

/// <summary>Read-only, bounded comparison for small text configuration files.</summary>
public sealed class LocalArtifactComparisonService : ILocalArtifactComparisonService
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    { ".ini", ".cfg", ".conf", ".json", ".xml", ".yaml", ".yml", ".toml", ".txt" };
    private static readonly string[] SensitiveKeys = ["password", "passwd", "token", "secret", "apikey", "api_key", "access_token", "refresh_token"];

    public async Task<LocalArtifactComparison> CompareAsync(string referencePath, string currentPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(referencePath)) return new(LocalArtifactComparisonStatus.ReferenceUnavailable);
        if (!File.Exists(currentPath)) return new(LocalArtifactComparisonStatus.CurrentUnavailable);
        if (!Extensions.Contains(Path.GetExtension(referencePath)) || !Extensions.Contains(Path.GetExtension(currentPath)))
            return new(LocalArtifactComparisonStatus.UnsupportedFormat);
        try
        {
            if (new FileInfo(referencePath).Length > MaxBytes || new FileInfo(currentPath).Length > MaxBytes)
                return new(LocalArtifactComparisonStatus.TooLarge);
            var reference = await ReadLinesAsync(referencePath, cancellationToken).ConfigureAwait(false);
            var current = await ReadLinesAsync(currentPath, cancellationToken).ConfigureAwait(false);
            var lcs = BuildLcs(reference, current);
            var removed = lcs.Count(x => x.Kind == DiffKind.Removed);
            var added = lcs.Count(x => x.Kind == DiffKind.Added);
            if (removed == 0 && added == 0) return new(LocalArtifactComparisonStatus.Identical);
            var lines = lcs.Select(x => new LocalArtifactDiffLine(x.LineNumber, Redact(x.Text), x.Kind == DiffKind.Removed, x.Kind == DiffKind.Added)).ToArray();
            return new(LocalArtifactComparisonStatus.Available, added, removed, Math.Min(added, removed), [new LocalArtifactDiffHunk(lines.First().LineNumber, lines)]);
        }
        catch (DecoderFallbackException) { return new(LocalArtifactComparisonStatus.UnsupportedFormat); }
        catch (IOException) { return new(LocalArtifactComparisonStatus.Unavailable); }
        catch (UnauthorizedAccessException) { return new(LocalArtifactComparisonStatus.Unavailable); }
    }

    private static async Task<string[]> ReadLinesAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        Encoding encoding = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE ? new UnicodeEncoding(false, true) :
            bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF ? new UnicodeEncoding(true, true) : new UTF8Encoding(false, true);
        var lines = encoding.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines.ToArray();
    }

    private static List<DiffEntry> BuildLcs(string[] left, string[] right)
    {
        var dp = new int[left.Length + 1, right.Length + 1];
        for (var i = left.Length - 1; i >= 0; i--)
            for (var j = right.Length - 1; j >= 0; j--)
                dp[i, j] = left[i] == right[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);
        var result = new List<DiffEntry>();
        var li = 0; var ri = 0;
        while (li < left.Length || ri < right.Length)
        {
            if (li < left.Length && ri < right.Length && left[li] == right[ri]) { li++; ri++; continue; }
            if (li < left.Length && (ri == right.Length || dp[li + 1, ri] >= dp[li, ri + 1])) result.Add(new(li + 1, left[li++], DiffKind.Removed));
            else result.Add(new(ri + 1, right[ri++], DiffKind.Added));
        }
        return result;
    }

    private static string Redact(string text)
    {
        var separator = text.IndexOf('=');
        if (separator <= 0) return text;
        var key = text[..separator].Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        return SensitiveKeys.Any(x => key.Contains(x, StringComparison.Ordinal)) ? text[..(separator + 1)] + "••••••••" : text;
    }

    private enum DiffKind { Removed, Added }
    private sealed record DiffEntry(int LineNumber, string Text, DiffKind Kind);
}
