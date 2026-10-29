using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;
using PlayStead.Core.Persistence;
using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.Core.Tests.LocalEnvironment;

public sealed class BeforeUpdateProtectionPolicyTests
{
    [Fact]
    public async Task Default_policy_is_Ask_and_Ask_exposes_a_prompt()
    {
        var fixture = Create(LocalEnvironmentEntryKind.Config);
        await fixture.Workflow.HandleUpdateAsync(fixture.State, CancellationToken.None);
        Assert.Single(fixture.Workflow.PendingPrompts);
        Assert.Equal(BeforeUpdateProtectionPolicy.Ask, await fixture.Policies.GetAsync(fixture.Installation.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Always_creates_before_update_snapshot()
    {
        var fixture = Create(LocalEnvironmentEntryKind.Config);
        await fixture.Policies.SetAsync(fixture.Installation.Id, BeforeUpdateProtectionPolicy.Always, CancellationToken.None);
        await fixture.Workflow.HandleUpdateAsync(fixture.State, CancellationToken.None);
        var snapshot = Assert.Single(fixture.Snapshots.Created);
        Assert.Equal(LocalFileSnapshotReason.BeforeUpdate, snapshot.Reason);
    }

    [Fact]
    public async Task Ask_accept_creates_snapshot_and_decline_creates_nothing()
    {
        var fixture = Create(LocalEnvironmentEntryKind.Config);
        await fixture.Workflow.HandleUpdateAsync(fixture.State, CancellationToken.None);
        var prompt = Assert.Single(fixture.Workflow.PendingPrompts);
        Assert.True(await fixture.Workflow.AcceptAsync(prompt.Id, CancellationToken.None));
        Assert.Single(fixture.Snapshots.Created);
        var second = Create(LocalEnvironmentEntryKind.Config);
        await second.Workflow.HandleUpdateAsync(second.State, CancellationToken.None);
        Assert.True(second.Workflow.Decline(Assert.Single(second.Workflow.PendingPrompts).Id));
        Assert.Empty(second.Snapshots.Created);
    }

    [Fact]
    public async Task Never_and_ineligible_entries_create_nothing()
    {
        var fixture = Create(LocalEnvironmentEntryKind.Save);
        await fixture.Policies.SetAsync(fixture.Installation.Id, BeforeUpdateProtectionPolicy.Never, CancellationToken.None);
        await fixture.Workflow.HandleUpdateAsync(fixture.State, CancellationToken.None);
        Assert.Empty(fixture.Workflow.PendingPrompts);
        Assert.Empty(fixture.Snapshots.Created);
    }

    [Fact]
    public async Task Duplicate_update_event_is_single_flight()
    {
        var fixture = Create(LocalEnvironmentEntryKind.Config);
        await fixture.Policies.SetAsync(fixture.Installation.Id, BeforeUpdateProtectionPolicy.Always, CancellationToken.None);
        await fixture.Workflow.HandleUpdateAsync(fixture.State, CancellationToken.None);
        await fixture.Workflow.HandleUpdateAsync(fixture.State, CancellationToken.None);
        Assert.Single(fixture.Snapshots.Created);
    }

    [Fact]
    public async Task Already_downloading_is_reported_without_claiming_protection()
    {
        var fixture = Create(LocalEnvironmentEntryKind.Config);
        var downloading = fixture.State with { Status = ProviderInstallUpdateStatus.Downloading };
        await fixture.Workflow.HandleUpdateAsync(downloading, CancellationToken.None);
        Assert.Contains("déjà en cours", fixture.Workflow.LastStatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Snapshots.Created);
    }

    private static Fixture Create(LocalEnvironmentEntryKind kind)
    {
        var game = GameId.New(); var installation = InstallationId.New();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ini");
        File.WriteAllText(path, "[Game]");
        var entry = new LocalEnvironmentEntry(Guid.NewGuid(), installation, kind, path, LocalEnvironmentPathKind.File, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.Catalog, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var install = new GameInstallation(installation, game, ProviderKind.Steam, "42", Path.GetDirectoryName(path)!, null, true, true, DateTimeOffset.UtcNow);
        var state = new ProviderInstallUpdateState(game, ProviderKind.Steam, "42", "1", "2", ProviderInstallUpdateStatus.UpdateAvailable, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        var discovery = new FakeDiscovery(installation, entry);
        var policies = new MemoryPolicies(); var snapshots = new FakeSnapshots();
        return new Fixture(install, state, policies, snapshots, new LocalEnvironmentBeforeUpdateProtectionService(new FakeLibraryStore(install), discovery, snapshots, policies));
    }

    private sealed record Fixture(GameInstallation Installation, ProviderInstallUpdateState State, MemoryPolicies Policies, FakeSnapshots Snapshots, LocalEnvironmentBeforeUpdateProtectionService Workflow);

    private sealed class FakeLibraryStore(GameInstallation installation) : ILibraryStore
    {
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(new LibrarySnapshot([new LogicalGame(installation.GameId, "Game", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)], [installation]));
        public Task ApplySourceScanAsync(PlayStead.Core.Scanning.SourceScanResult result, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeDiscovery(InstallationId id, LocalEnvironmentEntry entry) : ILocalEnvironmentDiscoveryService
    {
        public Task<GameLocalEnvironment> RefreshEnvironmentAsync(InstallationId installationId, CancellationToken cancellationToken) => GetEnvironmentAsync(installationId, cancellationToken);
        public Task<GameLocalEnvironment> GetEnvironmentAsync(InstallationId installationId, CancellationToken cancellationToken) => Task.FromResult(new GameLocalEnvironment(id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [entry]));
        public Task RemoveUserOverrideAsync(InstallationId installationId, LocalEnvironmentEntryKind kind, string path, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetConfigsAsync(InstallationId installationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([entry]);
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetLogsAsync(InstallationId installationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([]);
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetSavesAsync(InstallationId installationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([]);
        public Task<LocalEnvironmentCustomizationState> GetCustomizationStateAsync(InstallationId installationId, CancellationToken cancellationToken) => Task.FromResult(LocalEnvironmentCustomizationState.Indeterminate);
    }

    private sealed class MemoryPolicies : IBeforeUpdateProtectionPolicyStore
    {
        private readonly Dictionary<InstallationId, BeforeUpdateProtectionPolicy> _values = [];
        public Task<BeforeUpdateProtectionPolicy> GetAsync(InstallationId installationId, CancellationToken cancellationToken) => Task.FromResult(_values.GetValueOrDefault(installationId, BeforeUpdateProtectionPolicy.Ask));
        public Task SetAsync(InstallationId installationId, BeforeUpdateProtectionPolicy policy, CancellationToken cancellationToken) { _values[installationId] = policy; return Task.CompletedTask; }
    }

    private sealed class FakeSnapshots : ILocalEnvironmentSnapshotService
    {
        public List<LocalFileSnapshot> Created { get; } = [];
        public Task<LocalFileSnapshot> CreateSnapshotAsync(InstallationId installationId, Guid environmentEntryId, LocalFileSnapshotReason reason, CancellationToken cancellationToken) { var snapshot = new LocalFileSnapshot(Guid.NewGuid(), installationId, environmentEntryId, "settings.ini", DateTimeOffset.UtcNow, reason, null, 10, "hash", "snapshot", reason == LocalFileSnapshotReason.PreRestore); Created.Add(snapshot); return Task.FromResult(snapshot); }
        public Task<IReadOnlyList<LocalFileSnapshot>> GetSnapshotsAsync(InstallationId installationId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalFileSnapshot>>(Created);
        public Task<IReadOnlyList<LocalFileSnapshot>> GetSnapshotsForEntryAsync(Guid environmentEntryId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalFileSnapshot>>(Created);
        public Task DeleteSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<LocalFileSnapshot> RestoreSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken) => Task.FromResult(Created.Single(x => x.Id == snapshotId));
    }
}
