using PlayStead.Core.Library;

namespace PlayStead.Core.LocalEnvironment;

public sealed class LocalEnvironmentBaselineService : ILocalEnvironmentBaselineService
{
    private readonly ILocalEnvironmentDiscoveryService _environment;
    private readonly ILocalEnvironmentBaselineStore _store;
    private readonly TimeProvider _clock;

    public LocalEnvironmentBaselineService(ILocalEnvironmentDiscoveryService environment, ILocalEnvironmentBaselineStore store, TimeProvider? clock = null)
    {
        _environment = environment;
        _store = store;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<LocalEnvironmentBaseline> SetBaselineAsync(InstallationId installationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = await _environment.GetEnvironmentAsync(installationId, cancellationToken).ConfigureAwait(false);
        var now = _clock.GetUtcNow();
        var baseline = new LocalEnvironmentBaseline(installationId, now, now, current.Entries.Select(ToBaselineEntry).ToArray());
        await _store.ReplaceAsync(baseline, cancellationToken).ConfigureAwait(false);
        return baseline;
    }

    public async Task<IReadOnlyList<LocalEnvironmentComparison>> CompareWithBaselineAsync(InstallationId installationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var baseline = await _store.GetAsync(installationId, cancellationToken).ConfigureAwait(false);
        if (baseline is null) return [];
        var current = await _environment.GetEnvironmentAsync(installationId, cancellationToken).ConfigureAwait(false);
        var baselineMap = baseline.Entries.ToDictionary(EntryKey, StringComparer.OrdinalIgnoreCase);
        var currentEntries = current.Entries.Select(ToBaselineEntry).ToArray();
        var currentMap = currentEntries.ToDictionary(EntryKey, StringComparer.OrdinalIgnoreCase);
        var result = new List<LocalEnvironmentComparison>();
        foreach (var entry in currentEntries)
        {
            var key = EntryKey(entry);
            result.Add(baselineMap.TryGetValue(key, out var old)
                ? new LocalEnvironmentComparison(key, IsSignificantlyDifferent(old, entry) ? LocalEnvironmentComparisonState.Changed : LocalEnvironmentComparisonState.Unchanged, old, entry)
                : new LocalEnvironmentComparison(key, LocalEnvironmentComparisonState.New, null, entry));
        }
        foreach (var old in baseline.Entries)
        {
            var key = EntryKey(old);
            if (!currentMap.ContainsKey(key)) result.Add(new LocalEnvironmentComparison(key, LocalEnvironmentComparisonState.Missing, old, null));
        }
        return result.OrderBy(x => x.EntryKey, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static LocalEnvironmentBaselineEntry ToBaselineEntry(LocalEnvironmentEntry entry) =>
        new(entry.Kind, Path.GetFullPath(entry.Path), entry.PathKind, entry.Exists, entry.LastModifiedUtc, entry.Metadata is null ? null : new Dictionary<string, string>(entry.Metadata, StringComparer.OrdinalIgnoreCase));

    public static string EntryKey(LocalEnvironmentBaselineEntry entry)
    {
        var componentKind = entry.Metadata?.GetValueOrDefault("ComponentKind") ?? string.Empty;
        var componentName = entry.Metadata?.GetValueOrDefault("ComponentName") ?? string.Empty;
        return $"{entry.Kind}|{entry.NormalizedPath}|{componentKind}|{componentName}";
    }

    private static bool IsSignificantlyDifferent(LocalEnvironmentBaselineEntry old, LocalEnvironmentBaselineEntry current)
    {
        if (old.Exists != current.Exists) return true;
        if (old.LastModifiedUtc is not null && current.LastModifiedUtc is not null && old.LastModifiedUtc != current.LastModifiedUtc) return true;
        var keys = old.Kind switch
        {
            LocalEnvironmentEntryKind.Save => new[] { "FileCount", "TotalSizeBytes" },
            LocalEnvironmentEntryKind.Mod => new[] { "ComponentName", "Version", "DetectionEvidence" },
            _ => Array.Empty<string>()
        };
        foreach (var key in keys)
        {
            var oldValue = ReliableValue(old.Metadata, key);
            var currentValue = ReliableValue(current.Metadata, key);
            if (oldValue is not null && currentValue is not null && !string.Equals(oldValue, currentValue, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string? ReliableValue(IReadOnlyDictionary<string, string>? metadata, string key) =>
        metadata is not null && metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "Unknown", StringComparison.OrdinalIgnoreCase) ? value : null;
}
