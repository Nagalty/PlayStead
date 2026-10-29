using PlayStead.Core.Library;

namespace PlayStead.Core.LocalEnvironment;

public sealed class LocalEnvironmentDiscoveryService : ILocalEnvironmentDiscoveryService
{
    private readonly IReadOnlyList<ILocalEnvironmentContributor> _contributors;
    private readonly ILocalEnvironmentStore _store;
    private readonly Func<InstallationId, CancellationToken, Task<GameInstallation?>> _installationResolver;
    private readonly TimeProvider _clock;

    public LocalEnvironmentDiscoveryService(
        IEnumerable<ILocalEnvironmentContributor> contributors,
        ILocalEnvironmentStore store,
        Func<InstallationId, CancellationToken, Task<GameInstallation?>> installationResolver,
        TimeProvider? clock = null)
    {
        _contributors = contributors.ToArray();
        _store = store;
        _installationResolver = installationResolver;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<GameLocalEnvironment> GetEnvironmentAsync(InstallationId installationId, CancellationToken cancellationToken)
    {
        var persisted = await _store.GetEntriesAsync(installationId, cancellationToken).ConfigureAwait(false);
        var overrides = persisted.Where(x => x.IsUserOverride).ToArray();
        var overriddenKinds = overrides.Select(x => x.Kind).ToHashSet();
        var entries = persisted.Where(x => !x.IsUserOverride && !overriddenKinds.Contains(x.Kind)).Concat(overrides).ToArray();
        var now = _clock.GetUtcNow();
        return new GameLocalEnvironment(installationId, entries.Length == 0 ? now : entries.Min(x => x.LastObservedAtUtc), now, entries);
    }

    public async Task<GameLocalEnvironment> RefreshEnvironmentAsync(InstallationId installationId, CancellationToken cancellationToken)
    {
        var installation = await _installationResolver(installationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Installation '{installationId}' was not found.");
        var previous = await _store.GetEntriesAsync(installationId, cancellationToken).ConfigureAwait(false);
        var detected = new List<LocalEnvironmentEntry>();
        foreach (var contributor in _contributors)
        {
            detected.AddRange(await contributor.DiscoverAsync(installation, cancellationToken).ConfigureAwait(false));
        }

        var now = _clock.GetUtcNow();
        var previousByPath = new Dictionary<(LocalEnvironmentEntryKind Kind, LocalEnvironmentPathKind PathKind, string Path), LocalEnvironmentEntry>(EntryKeyComparer.Instance);
        foreach (var prior in previous.Where(x => !x.IsUserOverride))
        {
            previousByPath.TryAdd((prior.Kind, prior.PathKind, prior.Path), prior);
            if (LocalEnvironmentPathNormalizer.TryNormalize(prior.Path, installation, out var normalizedPrior))
                previousByPath.TryAdd((prior.Kind, prior.PathKind, normalizedPrior), prior);
        }
        var normalized = detected.Select(entry => NormalizeAndObserveSafe(entry, installation, now, cancellationToken))
            .Where(x => x is not null).Select(x => x!)
            .Select(entry => previousByPath.TryGetValue((entry.Kind, entry.PathKind, entry.Path), out var prior) ? entry with { Id = prior.Id } : entry)
            .GroupBy(x => (x.Kind, x.PathKind, x.Path), EntryKeyComparer.Instance)
            .Select(MergeEntries)
            .ToArray();
        var overrides = previous.Where(x => x.IsUserOverride).ToArray();
        var overriddenKinds = overrides.Select(x => x.Kind).ToHashSet();
        var merged = normalized.Where(x => !overriddenKinds.Contains(x.Kind)).Concat(overrides).ToArray();
        // Keep the detected baseline persisted even while an override masks it;
        // clearing the override can then reveal the latest detected value.
        await _store.ReplaceDetectedAsync(installationId, normalized, cancellationToken).ConfigureAwait(false);
        foreach (var entry in overrides)
        {
            await _store.UpsertUserOverrideAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        return new GameLocalEnvironment(installationId, now, now, merged);
    }

    public Task RemoveUserOverrideAsync(
        InstallationId installationId,
        LocalEnvironmentEntryKind kind,
        string path,
        CancellationToken cancellationToken)
        => _store.RemoveUserOverrideAsync(installationId, kind, path, cancellationToken);

    public async Task<IReadOnlyList<LocalEnvironmentEntry>> GetConfigsAsync(InstallationId installationId, CancellationToken cancellationToken) =>
        (await GetEnvironmentAsync(installationId, cancellationToken).ConfigureAwait(false)).Entries.Where(x => x.Kind == LocalEnvironmentEntryKind.Config).ToArray();

    public async Task<IReadOnlyList<LocalEnvironmentEntry>> GetLogsAsync(InstallationId installationId, CancellationToken cancellationToken) =>
        (await GetEnvironmentAsync(installationId, cancellationToken).ConfigureAwait(false)).Entries.Where(x => x.Kind == LocalEnvironmentEntryKind.Log).ToArray();

    public async Task<IReadOnlyList<LocalEnvironmentEntry>> GetSavesAsync(InstallationId installationId, CancellationToken cancellationToken) =>
        (await GetEnvironmentAsync(installationId, cancellationToken).ConfigureAwait(false)).Entries.Where(x => x.Kind == LocalEnvironmentEntryKind.Save).ToArray();

    public async Task<LocalEnvironmentCustomizationState> GetCustomizationStateAsync(InstallationId installationId, CancellationToken cancellationToken)
    {
        var entries = await GetModsAsync(installationId, cancellationToken).ConfigureAwait(false);
        if (entries.Any(x => x.Metadata?.TryGetValue("VanillaProof", out var proof) == true && string.Equals(proof, "Confirmed", StringComparison.OrdinalIgnoreCase)))
            return LocalEnvironmentCustomizationState.VanillaConfirmed;
        return entries.Any(x => x.Metadata?.TryGetValue("DetectionEvidence", out var evidence) == true && !string.IsNullOrWhiteSpace(evidence))
            ? LocalEnvironmentCustomizationState.Customized
            : LocalEnvironmentCustomizationState.Indeterminate;
    }

    public async Task<IReadOnlyList<LocalEnvironmentEntry>> GetModsAsync(InstallationId installationId, CancellationToken cancellationToken) =>
        (await GetEnvironmentAsync(installationId, cancellationToken).ConfigureAwait(false)).Entries.Where(x => x.Kind == LocalEnvironmentEntryKind.Mod).ToArray();

    private static LocalEnvironmentEntry NormalizeAndObserve(LocalEnvironmentEntry entry, GameInstallation installation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var canResolve = LocalEnvironmentPathNormalizer.TryNormalize(entry.Path, installation, out var path);
        var exists = canResolve && (entry.PathKind == LocalEnvironmentPathKind.Pattern
            ? Directory.Exists(Path.GetDirectoryName(path)) || File.Exists(path)
            : entry.PathKind == LocalEnvironmentPathKind.Directory ? Directory.Exists(path) : File.Exists(path));
        DateTimeOffset? modified = null;
        var metadata = entry.Metadata is null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(entry.Metadata, StringComparer.OrdinalIgnoreCase);
        if (entry.Kind == LocalEnvironmentEntryKind.Save && canResolve)
        {
            var scan = LocalEnvironmentSaveScanner.Scan(path, entry.PathKind, cancellationToken);
            exists = scan.Exists;
            modified = scan.LastModifiedUtc;
            foreach (var item in scan.Metadata)
                if (!metadata.ContainsKey(item.Key)) metadata[item.Key] = item.Value;
        }
        else if (exists)
        {
            modified = entry.PathKind == LocalEnvironmentPathKind.Directory ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path);
        }

        return entry with { InstallationId = installation.Id, Path = path, Exists = exists, LastObservedAtUtc = now, LastModifiedUtc = modified, Metadata = metadata };
    }

    private static LocalEnvironmentEntry? NormalizeAndObserveSafe(LocalEnvironmentEntry entry, GameInstallation installation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return NormalizeAndObserve(entry, installation, now, cancellationToken);
        }
        catch (IOException) { return entry with { InstallationId = installation.Id, Exists = false, LastObservedAtUtc = now }; }
        catch (UnauthorizedAccessException) { return entry with { InstallationId = installation.Id, Exists = false, LastObservedAtUtc = now }; }
    }

    private static LocalEnvironmentEntry MergeEntries(IEnumerable<LocalEnvironmentEntry> entries)
    {
        var ordered = entries.OrderByDescending(x => ConfidenceRank(x.Confidence)).ThenByDescending(x => SourceRank(x.Source)).ToArray();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in ordered.Reverse())
            if (entry.Metadata is not null)
                foreach (var item in entry.Metadata) metadata[item.Key] = item.Value;
        var evidence = ordered.SelectMany(x => x.Metadata is not null && x.Metadata.TryGetValue("DetectionEvidence", out var value) ? value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (evidence.Length > 0) metadata["DetectionEvidence"] = string.Join(";", evidence);
        var version = ordered.Select(x => x.Metadata is not null && x.Metadata.TryGetValue("Version", out var value) ? value : null).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (version is not null) metadata["Version"] = version;
        return ordered[0] with { Metadata = metadata };
    }

    private static int ConfidenceRank(LocalEnvironmentConfidence confidence) => confidence switch
    {
        LocalEnvironmentConfidence.Confirmed => 3,
        LocalEnvironmentConfidence.Detected => 2,
        _ => 1
    };

    private static int SourceRank(LocalEnvironmentSource source) => source switch
    {
        LocalEnvironmentSource.User => 6,
        LocalEnvironmentSource.Provider => 5,
        LocalEnvironmentSource.Catalog => 4,
        LocalEnvironmentSource.CommunityRegistry => 3,
        LocalEnvironmentSource.SystemDetection => 2,
        _ => 1
    };

    private sealed class EntryKeyComparer : IEqualityComparer<(LocalEnvironmentEntryKind Kind, LocalEnvironmentPathKind PathKind, string Path)>
    {
        public static EntryKeyComparer Instance { get; } = new();
        public bool Equals((LocalEnvironmentEntryKind Kind, LocalEnvironmentPathKind PathKind, string Path) x, (LocalEnvironmentEntryKind Kind, LocalEnvironmentPathKind PathKind, string Path) y) => x.Kind == y.Kind && x.PathKind == y.PathKind && StringComparer.OrdinalIgnoreCase.Equals(x.Path, y.Path);
        public int GetHashCode((LocalEnvironmentEntryKind Kind, LocalEnvironmentPathKind PathKind, string Path) obj) => HashCode.Combine(obj.Kind, obj.PathKind, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path));
    }
}
