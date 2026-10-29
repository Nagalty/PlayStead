using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;

namespace PlayStead.Core.Tests.LocalEnvironment;

public sealed class LocalEnvironmentBaselineServiceTests
{
    [Fact]
    public async Task Baseline_compare_and_rebaseline_are_deterministic()
    {
        var installation = Installation();
        var environment = new FakeEnvironment(installation.Id, [Entry(LocalEnvironmentEntryKind.Config, "config.ini", true, lastModifiedUtc: DateTimeOffset.Parse("2026-01-01T00:00:00Z"))]);
        var store = new MemoryBaselineStore();
        var service = new LocalEnvironmentBaselineService(environment, store);

        await service.SetBaselineAsync(installation.Id, CancellationToken.None);
        Assert.Equal(LocalEnvironmentComparisonState.Unchanged, Assert.Single(await service.CompareWithBaselineAsync(installation.Id, CancellationToken.None)).State);
        environment.Entries = [Entry(LocalEnvironmentEntryKind.Config, "config.ini", true, lastModifiedUtc: DateTimeOffset.Parse("2026-01-02T00:00:00Z")), Entry(LocalEnvironmentEntryKind.Log, "new.log", true)];
        var changed = await service.CompareWithBaselineAsync(installation.Id, CancellationToken.None);
        Assert.Contains(changed, x => x.State == LocalEnvironmentComparisonState.Changed);
        Assert.Contains(changed, x => x.State == LocalEnvironmentComparisonState.New);
        await service.SetBaselineAsync(installation.Id, CancellationToken.None);
        Assert.All(await service.CompareWithBaselineAsync(installation.Id, CancellationToken.None), x => Assert.Equal(LocalEnvironmentComparisonState.Unchanged, x.State));
    }

    [Fact]
    public async Task Missing_entry_is_not_dropped_and_save_metadata_changes_are_detected()
    {
        var installation = Installation();
        var environment = new FakeEnvironment(installation.Id, [Entry(LocalEnvironmentEntryKind.Save, "save", true, metadata: new Dictionary<string, string>() { ["FileCount"] = "1", ["TotalSizeBytes"] = "10" }), Entry(LocalEnvironmentEntryKind.Mod, "mod", true)]);
        var store = new MemoryBaselineStore();
        var service = new LocalEnvironmentBaselineService(environment, store);
        await service.SetBaselineAsync(installation.Id, CancellationToken.None);
        environment.Entries = [Entry(LocalEnvironmentEntryKind.Save, "save", true, metadata: new Dictionary<string, string>() { ["FileCount"] = "2", ["TotalSizeBytes"] = "20" })];
        var result = await service.CompareWithBaselineAsync(installation.Id, CancellationToken.None);
        Assert.Contains(result, x => x.State == LocalEnvironmentComparisonState.Changed);
        Assert.Contains(result, x => x.State == LocalEnvironmentComparisonState.Missing && x.BaselineEntry!.Kind == LocalEnvironmentEntryKind.Mod);
    }

    [Fact]
    public async Task Mod_version_change_is_detected_but_unknown_values_are_not_changes()
    {
        var installation = Installation();
        var environment = new FakeEnvironment(installation.Id, [Entry(LocalEnvironmentEntryKind.Mod, "mod", true, metadata: new Dictionary<string, string>() { ["ComponentKind"] = "BepInEx", ["Version"] = "Unknown" }, lastModifiedUtc: null)]);
        var store = new MemoryBaselineStore();
        var service = new LocalEnvironmentBaselineService(environment, store);
        await service.SetBaselineAsync(installation.Id, CancellationToken.None);
        environment.Entries = [Entry(LocalEnvironmentEntryKind.Mod, "mod", true, metadata: new Dictionary<string, string>() { ["ComponentKind"] = "BepInEx", ["Version"] = "1.0" }, lastModifiedUtc: null)];
        Assert.Equal(LocalEnvironmentComparisonState.Unchanged, Assert.Single(await service.CompareWithBaselineAsync(installation.Id, CancellationToken.None)).State);
        await service.SetBaselineAsync(installation.Id, CancellationToken.None);
        environment.Entries = [Entry(LocalEnvironmentEntryKind.Mod, "mod", true, metadata: new Dictionary<string, string>() { ["ComponentKind"] = "BepInEx", ["Version"] = "2.0" }, lastModifiedUtc: null)];
        Assert.Equal(LocalEnvironmentComparisonState.Changed, Assert.Single(await service.CompareWithBaselineAsync(installation.Id, CancellationToken.None)).State);
    }

    [Fact]
    public async Task Baselines_are_isolated_by_installation_and_override_state_is_captured()
    {
        var first = Installation();
        var second = Installation(first.GameId);
        var environment = new FakeEnvironment(first.Id, [Entry(LocalEnvironmentEntryKind.Save, "a", true, userOverride: true)]);
        var store = new MemoryBaselineStore();
        var service = new LocalEnvironmentBaselineService(environment, store);
        await service.SetBaselineAsync(first.Id, CancellationToken.None);
        environment.InstallationId = second.Id;
        environment.Entries = [Entry(LocalEnvironmentEntryKind.Save, "b", true)];
        await service.SetBaselineAsync(second.Id, CancellationToken.None);
        Assert.NotNull(await store.GetAsync(first.Id, CancellationToken.None));
        Assert.NotEqual(store.Baselines[first.Id].Entries[0].NormalizedPath, store.Baselines[second.Id].Entries[0].NormalizedPath);
    }

    [Fact]
    public async Task Cancellation_is_safe()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var service = new LocalEnvironmentBaselineService(new FakeEnvironment(Installation().Id, []), new MemoryBaselineStore());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SetBaselineAsync(Installation().Id, source.Token));
    }

    private static GameInstallation Installation(GameId? game = null) => new(InstallationId.New(), game ?? GameId.New(), ProviderKind.Manual, "fixture", Path.GetTempPath(), null, true, true, DateTimeOffset.UtcNow);
    private static LocalEnvironmentEntry Entry(LocalEnvironmentEntryKind kind, string path, bool exists, IReadOnlyDictionary<string, string>? metadata = null, bool userOverride = false, DateTimeOffset? lastModifiedUtc = null) => new(Guid.NewGuid(), InstallationId.New(), kind, Path.Combine(Path.GetTempPath(), path), LocalEnvironmentPathKind.File, LocalEnvironmentConfidence.Detected, userOverride ? LocalEnvironmentSource.User : LocalEnvironmentSource.Catalog, exists, DateTimeOffset.UtcNow, lastModifiedUtc, metadata, userOverride);

    private sealed class FakeEnvironment(InstallationId installationId, IReadOnlyList<LocalEnvironmentEntry> entries) : ILocalEnvironmentDiscoveryService
    {
        public InstallationId InstallationId { get; set; } = installationId;
        public IReadOnlyList<LocalEnvironmentEntry> Entries { get; set; } = entries;
        public Task<GameLocalEnvironment> GetEnvironmentAsync(InstallationId id, CancellationToken token) => Task.FromResult(new GameLocalEnvironment(id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Entries.Select(x => x with { InstallationId = id }).ToArray()));
        public Task<GameLocalEnvironment> RefreshEnvironmentAsync(InstallationId id, CancellationToken token) => GetEnvironmentAsync(id, token);
        public Task RemoveUserOverrideAsync(InstallationId id, LocalEnvironmentEntryKind kind, string path, CancellationToken token) => Task.CompletedTask;
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetConfigsAsync(InstallationId id, CancellationToken token) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([]);
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetLogsAsync(InstallationId id, CancellationToken token) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([]);
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetSavesAsync(InstallationId id, CancellationToken token) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([]);
        public Task<LocalEnvironmentCustomizationState> GetCustomizationStateAsync(InstallationId id, CancellationToken token) => Task.FromResult(LocalEnvironmentCustomizationState.Indeterminate);
    }

    private sealed class MemoryBaselineStore : ILocalEnvironmentBaselineStore
    {
        public Dictionary<InstallationId, LocalEnvironmentBaseline> Baselines { get; } = [];
        public Task<LocalEnvironmentBaseline?> GetAsync(InstallationId id, CancellationToken token) => Task.FromResult(Baselines.GetValueOrDefault(id));
        public Task ReplaceAsync(LocalEnvironmentBaseline baseline, CancellationToken token) { Baselines[baseline.InstallationId] = baseline; return Task.CompletedTask; }
    }
}
