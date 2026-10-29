using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;
using PlayStead.Core.Persistence;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailLocalEnvironmentSectionTests
{
    [Fact]
    public async Task Empty_environment_shows_compact_empty_state()
    {
        var fixture = CreateFixture([]);
        await fixture.ViewModel.LoadAsync(fixture.GameId, fixture.InstallPath, CancellationToken.None);
        Assert.True(fixture.ViewModel.IsVisible);
        Assert.True(fixture.ViewModel.HasEmptyState);
    }

    [Fact]
    public async Task Configs_and_valid_path_project_summary_and_action()
    {
        var path = Path.Combine(Path.GetTempPath(), "settings.ini");
        await File.WriteAllTextAsync(path, "[Game]");
        try
        {
            var fixture = CreateFixture([Entry(LocalEnvironmentEntryKind.Config, path, true)]);
            await fixture.ViewModel.LoadAsync(fixture.GameId, fixture.InstallPath, CancellationToken.None);
            Assert.True(fixture.ViewModel.IsVisible);
            Assert.Equal("1 fichier présent", fixture.ViewModel.ConfigSummary);
            Assert.Single(fixture.ViewModel.ConfigEntries);
            Assert.True(fixture.ViewModel.ConfigEntries[0].CanOpen);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Load_refreshes_discovery_before_reading_the_environment()
    {
        var fixture = CreateFixture([Entry(LocalEnvironmentEntryKind.Config, "settings.ini", false)]);
        await fixture.ViewModel.LoadAsync(fixture.GameId, fixture.InstallPath, CancellationToken.None);
        Assert.True(fixture.Discovery.RefreshCalled);
    }

    [Fact]
    public async Task Indeterminate_customization_is_not_called_vanilla_and_baseline_counts_are_projected()
    {
        var fixture = CreateFixture([Entry(LocalEnvironmentEntryKind.Save, "save", true)]);
        await fixture.ViewModel.LoadAsync(fixture.GameId, fixture.InstallPath, CancellationToken.None);
        Assert.Equal("État indéterminé", fixture.ViewModel.CustomizationLabel);
        Assert.Equal("PlayStead n’a pas encore d’état de référence pour cette installation.", fixture.ViewModel.BaselineSummary);
    }

    [Fact]
    public async Task Two_installations_same_game_use_install_path_isolation()
    {
        var gameId = GameId.New();
        var firstPath = Path.Combine(Path.GetTempPath(), "first");
        var secondPath = Path.Combine(Path.GetTempPath(), "second");
        var first = Entry(LocalEnvironmentEntryKind.Log, Path.Combine(firstPath, "game.log"), true) with { InstallationId = InstallationId.New() };
        var second = Entry(LocalEnvironmentEntryKind.Config, Path.Combine(secondPath, "settings.ini"), true) with { InstallationId = InstallationId.New() };
        var store = new FakeLibraryStore(gameId, first.InstallationId, firstPath, second.InstallationId, secondPath);
        var discovery = new FakeDiscovery(first.InstallationId, [first]); discovery.Add(second.InstallationId, [second]);
        var vm = new LocalEnvironmentSectionViewModel(store, discovery, new FakeBaselineStore());
        await vm.LoadAsync(gameId, secondPath, CancellationToken.None);
        Assert.Single(vm.ConfigEntries);
        Assert.Empty(vm.LogEntries);
    }

    [Fact]
    public async Task Cancellation_during_load_is_propagated_safely()
    {
        var fixture = CreateFixture([Entry(LocalEnvironmentEntryKind.Config, "settings.ini", true)]);
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.ViewModel.LoadAsync(fixture.GameId, fixture.InstallPath, source.Token));
    }

    [Fact]
    public async Task Snapshot_manager_lists_newest_first_and_formats_reason_and_size()
    {
        var path = Path.Combine(Path.GetTempPath(), "settings.ini");
        await File.WriteAllTextAsync(path, "[Game]");
        try
        {
            var entry = Entry(LocalEnvironmentEntryKind.Config, path, true);
            var installation = entry.InstallationId;
            var gameId = GameId.New();
            var older = new LocalFileSnapshot(Guid.NewGuid(), installation, entry.Id, path, DateTimeOffset.UtcNow.AddMinutes(-2), LocalFileSnapshotReason.Manual, DateTimeOffset.UtcNow, 2048, "A", "older", false);
            var newer = new LocalFileSnapshot(Guid.NewGuid(), installation, entry.Id, path, DateTimeOffset.UtcNow, LocalFileSnapshotReason.PreRestore, DateTimeOffset.UtcNow, 4096, "B", "newer", true);
            var discovery = new FakeDiscovery(installation, [entry]);
            var service = new FakeSnapshotService([older, newer]);
            var vm = new LocalEnvironmentSectionViewModel(new FakeLibraryStore(gameId, installation, Path.GetDirectoryName(path)!), discovery, new FakeBaselineStore(), service, null, _ => Task.FromResult(true));

            await vm.LoadAsync(gameId, Path.GetDirectoryName(path)!, CancellationToken.None);

            Assert.Equal([newer.Id, older.Id], vm.Snapshots.Select(x => x.Id));
            Assert.Equal("État avant restauration", vm.Snapshots[0].ReasonLabel);
            Assert.Equal("Créée manuellement", vm.Snapshots[1].ReasonLabel);
            Assert.Equal("4 Ko", vm.Snapshots[0].SizeLabel);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Before_update_snapshot_is_labelled_without_exposing_backend_enum()
    {
        var path = Path.Combine(Path.GetTempPath(), "settings.ini");
        await File.WriteAllTextAsync(path, "[Game]");
        try
        {
            var entry = Entry(LocalEnvironmentEntryKind.Config, path, true);
            var snapshot = new LocalFileSnapshot(Guid.NewGuid(), entry.InstallationId, entry.Id, path, DateTimeOffset.UtcNow, LocalFileSnapshotReason.BeforeUpdate, null, 10, "A", "before", false);
            var gameId = GameId.New();
            var vm = new LocalEnvironmentSectionViewModel(new FakeLibraryStore(gameId, entry.InstallationId, Path.GetDirectoryName(path)!), new FakeDiscovery(entry.InstallationId, [entry]), new FakeBaselineStore(), new FakeSnapshotService([snapshot]));
            await vm.LoadAsync(gameId, Path.GetDirectoryName(path)!, CancellationToken.None);
            Assert.Equal("Avant mise à jour", vm.Snapshots.Single().ReasonLabel);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Before_update_policy_is_loaded_and_persisted_per_installation()
    {
        var fixture = CreateFixture([Entry(LocalEnvironmentEntryKind.Config, "settings.ini", true)]);
        var policies = new FakePolicyStore();
        var vm = new LocalEnvironmentSectionViewModel(new FakeLibraryStore(fixture.GameId, fixture.Discovery.InstallationId, fixture.InstallPath), fixture.Discovery, new FakeBaselineStore(), policyStore: policies);
        await vm.LoadAsync(fixture.GameId, fixture.InstallPath, CancellationToken.None);
        Assert.Equal(BeforeUpdateProtectionPolicy.Ask, vm.BeforeUpdatePolicy);
        await vm.SetBeforeUpdatePolicyCommand.ExecuteAsync(new BeforeUpdateProtectionPolicyOption(BeforeUpdateProtectionPolicy.Never, "Jamais"));
        Assert.Equal(BeforeUpdateProtectionPolicy.Never, await policies.GetAsync(fixture.Discovery.InstallationId, CancellationToken.None));
    }

    [Fact]
    public async Task Create_snapshot_refreshes_manager_and_uses_manual_reason()
    {
        var path = Path.Combine(Path.GetTempPath(), "settings.ini");
        await File.WriteAllTextAsync(path, "[Game]");
        try
        {
            var entry = Entry(LocalEnvironmentEntryKind.Config, path, true);
            var gameId = GameId.New();
            var service = new FakeSnapshotService([]);
            var vm = new LocalEnvironmentSectionViewModel(new FakeLibraryStore(gameId, entry.InstallationId, Path.GetDirectoryName(path)!), new FakeDiscovery(entry.InstallationId, [entry]), new FakeBaselineStore(), service, null, _ => Task.FromResult(true));
            await vm.LoadAsync(gameId, Path.GetDirectoryName(path)!, CancellationToken.None);
            await vm.CreateSnapshotCommand.ExecuteAsync(vm.ConfigEntries[0]);
            Assert.Single(vm.Snapshots);
            Assert.Equal("Créée manuellement", vm.Snapshots[0].ReasonLabel);
            Assert.Equal("Réglages protégés.", vm.SnapshotFeedback);
        }
        finally { File.Delete(path); }
    }

    private static Fixture CreateFixture(IReadOnlyList<LocalEnvironmentEntry> entries, LocalEnvironmentBaseline? baseline = null)
    {
        var gameId = GameId.New(); var installationId = InstallationId.New(); var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var discovery = new FakeDiscovery(installationId, entries);
        return new Fixture(gameId, path, new LocalEnvironmentSectionViewModel(new FakeLibraryStore(gameId, installationId, path), discovery, new FakeBaselineStore(baseline)), discovery);
    }

    private static LocalEnvironmentEntry Entry(LocalEnvironmentEntryKind kind, string path, bool exists) => new(Guid.NewGuid(), InstallationId.New(), kind, path, LocalEnvironmentPathKind.File, LocalEnvironmentConfidence.Detected, LocalEnvironmentSource.Catalog, exists, DateTimeOffset.UtcNow, exists ? DateTimeOffset.UtcNow : null);

    private sealed record Fixture(GameId GameId, string InstallPath, LocalEnvironmentSectionViewModel ViewModel, FakeDiscovery Discovery);

    private sealed class FakeLibraryStore(GameId gameId, InstallationId firstId, string firstPath, InstallationId? secondId = null, string? secondPath = null) : ILibraryStore
    {
        public Task ApplySourceScanAsync(PlayStead.Core.Scanning.SourceScanResult result, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
        {
            var installations = secondId is null
                ? new[] { new GameInstallation(firstId, gameId, ProviderKind.Manual, "first", firstPath, null, true, true, DateTimeOffset.UtcNow) }
                : new[]
                {
                    new GameInstallation(firstId, gameId, ProviderKind.Manual, "first", firstPath, null, true, true, DateTimeOffset.UtcNow),
                    new GameInstallation(secondId.Value, gameId, ProviderKind.Manual, "second", secondPath!, null, false, true, DateTimeOffset.UtcNow)
                };
            return Task.FromResult(new LibrarySnapshot([], installations));
        }
    }

    private sealed class FakeDiscovery(InstallationId id, IReadOnlyList<LocalEnvironmentEntry> initial) : ILocalEnvironmentDiscoveryService
    {
        public InstallationId InstallationId { get; } = id;
        public bool RefreshCalled { get; private set; }
        private readonly Dictionary<InstallationId, IReadOnlyList<LocalEnvironmentEntry>> _values = new() { [id] = initial };
        public void Add(InstallationId key, IReadOnlyList<LocalEnvironmentEntry> value) => _values[key] = value;
        public Task<GameLocalEnvironment> GetEnvironmentAsync(InstallationId installationId, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(new GameLocalEnvironment(installationId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, _values.GetValueOrDefault(installationId, []))); }
        public Task<GameLocalEnvironment> RefreshEnvironmentAsync(InstallationId installationId, CancellationToken token) { RefreshCalled = true; return GetEnvironmentAsync(installationId, token); }
        public Task RemoveUserOverrideAsync(InstallationId installationId, LocalEnvironmentEntryKind kind, string path, CancellationToken token) => Task.CompletedTask;
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetConfigsAsync(InstallationId installationId, CancellationToken token) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([]);
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetLogsAsync(InstallationId installationId, CancellationToken token) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([]);
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetSavesAsync(InstallationId installationId, CancellationToken token) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([]);
        public Task<LocalEnvironmentCustomizationState> GetCustomizationStateAsync(InstallationId installationId, CancellationToken token) => Task.FromResult(LocalEnvironmentCustomizationState.Indeterminate);
    }

    private sealed class FakeBaselineStore(LocalEnvironmentBaseline? baseline = null) : ILocalEnvironmentBaselineStore
    {
        private readonly LocalEnvironmentBaseline? _baseline = baseline;
        public Task<LocalEnvironmentBaseline?> GetAsync(InstallationId installationId, CancellationToken token) => Task.FromResult(_baseline);
        public Task ReplaceAsync(LocalEnvironmentBaseline baseline, CancellationToken token) => Task.CompletedTask;
    }

    private sealed class FakePolicyStore : IBeforeUpdateProtectionPolicyStore
    {
        private readonly Dictionary<InstallationId, BeforeUpdateProtectionPolicy> _values = [];
        public Task<BeforeUpdateProtectionPolicy> GetAsync(InstallationId installationId, CancellationToken token) => Task.FromResult(_values.GetValueOrDefault(installationId, BeforeUpdateProtectionPolicy.Ask));
        public Task SetAsync(InstallationId installationId, BeforeUpdateProtectionPolicy policy, CancellationToken token) { _values[installationId] = policy; return Task.CompletedTask; }
    }

    private sealed class FakeSnapshotStore : ILocalFileSnapshotStore
    {
        public Task<LocalFileSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<LocalFileSnapshot?>(null);
        public Task<IReadOnlyList<LocalFileSnapshot>> GetAsync(InstallationId installationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalFileSnapshot>>([]);
        public Task<IReadOnlyList<LocalFileSnapshot>> GetForEntryAsync(Guid environmentEntryId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalFileSnapshot>>([]);
        public Task AddAsync(LocalFileSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSnapshotService(IReadOnlyList<LocalFileSnapshot> initial) : ILocalEnvironmentSnapshotService
    {
        private readonly List<LocalFileSnapshot> _snapshots = initial.ToList();
        public Task<LocalFileSnapshot> CreateSnapshotAsync(InstallationId installationId, Guid environmentEntryId, LocalFileSnapshotReason reason, CancellationToken cancellationToken)
        {
            var created = new LocalFileSnapshot(Guid.NewGuid(), installationId, environmentEntryId, "settings.ini", DateTimeOffset.UtcNow, reason, null, 10, "C", "created", reason == LocalFileSnapshotReason.PreRestore);
            _snapshots.Add(created); return Task.FromResult(created);
        }
        public Task<IReadOnlyList<LocalFileSnapshot>> GetSnapshotsAsync(InstallationId installationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalFileSnapshot>>(_snapshots.Where(x => x.InstallationId == installationId).ToArray());
        public Task<IReadOnlyList<LocalFileSnapshot>> GetSnapshotsForEntryAsync(Guid environmentEntryId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalFileSnapshot>>(_snapshots.Where(x => x.EnvironmentEntryId == environmentEntryId).ToArray());
        public Task DeleteSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken) { _snapshots.RemoveAll(x => x.Id == snapshotId); return Task.CompletedTask; }
        public Task<LocalFileSnapshot> RestoreSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken) => Task.FromResult(_snapshots.Single(x => x.Id == snapshotId));
    }
}
