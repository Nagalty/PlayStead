using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;

namespace PlayStead.Core.Tests.LocalArtifacts;

public sealed class LocalProtectionSetupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-Protection-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Protect_all_creates_baselines_and_initial_save_snapshot_idempotently()
    {
        Directory.CreateDirectory(_root);
        var game = GameId.New();
        var config = Artifact(game, GameLocalArtifactKind.Configuration, "config");
        var save = Artifact(game, GameLocalArtifactKind.SaveData, "save");
        var discovery = new FakeDiscovery(config, save);
        var baselines = new MemoryBaselines();
        var snapshots = new MemorySnapshots(_root);
        var sut = new LocalProtectionSetupService(discovery, new Sha256ArtifactFingerprintService(), baselines, snapshots.Service);

        var first = await sut.ProtectAsync([new(game, ProviderKind.Steam, "1")], CancellationToken.None);
        var second = await sut.ProtectAsync([new(game, ProviderKind.Steam, "1")], CancellationToken.None);

        Assert.Equal(2, first.Protected);
        Assert.Equal(2, second.AlreadyProtected);
        var inventory = await sut.InspectAsync([new(game, ProviderKind.Steam, "1")], CancellationToken.None);
        Assert.All(inventory.Artifacts, artifact => Assert.Equal(LocalProtectionState.Protected, artifact.State));
        Assert.Single(snapshots.Items, x => x.Reason == SnapshotReason.InitialProtection);
        Assert.Equal(2, baselines.Items.Count);
    }

    [Fact]
    public async Task User_defined_artifacts_are_excluded_from_global_protection()
    {
        Directory.CreateDirectory(_root);
        var game = GameId.New();
        var custom = Artifact(game, GameLocalArtifactKind.SaveData, "custom") with { Source = GameLocalArtifactSource.UserDefined };
        var baselines = new MemoryBaselines();
        var snapshots = new MemorySnapshots(_root);
        var sut = new LocalProtectionSetupService(new FakeDiscovery(custom), new Sha256ArtifactFingerprintService(), baselines, snapshots.Service);

        var result = await sut.ProtectAsync([new(game, ProviderKind.Steam, "1")], CancellationToken.None);

        Assert.Equal(0, result.Protected);
        Assert.Empty(baselines.Items);
        Assert.Empty(snapshots.Items);
    }

    [Fact]
    public async Task Inspect_counts_only_games_with_recognized_artifacts_once()
    {
        Directory.CreateDirectory(_root);
        var recognized = GameId.New();
        var empty = GameId.New();
        var config = Artifact(recognized, GameLocalArtifactKind.Configuration, "config-count");
        var save = Artifact(recognized, GameLocalArtifactKind.SaveData, "save-count");
        var discovery = new SelectiveDiscovery((recognized, new[] { config, save }));
        var sut = new LocalProtectionSetupService(discovery, new Sha256ArtifactFingerprintService(), new MemoryBaselines(), new MemorySnapshots(_root).Service);

        var inventory = await sut.InspectAsync(
            [new(recognized, ProviderKind.Steam, "recognized", "Recognized"), new(empty, ProviderKind.Steam, "empty", "Empty")],
            CancellationToken.None);

        Assert.Equal(1, inventory.RecognizedGamesCount);
        Assert.Equal(2, inventory.RecognizedArtifactsCount);
        Assert.All(inventory.Artifacts, x => Assert.Equal(recognized, x.Artifact.GameId));
    }

    [Fact]
    public async Task Logs_are_excluded_from_global_protection()
    {
        Directory.CreateDirectory(_root);
        var game = GameId.New();
        var log = Artifact(game, GameLocalArtifactKind.Log, "log");
        var baselines = new MemoryBaselines();
        var snapshots = new MemorySnapshots(_root);
        var sut = new LocalProtectionSetupService(new FakeDiscovery(log), new Sha256ArtifactFingerprintService(), baselines, snapshots.Service);

        var inventory = await sut.InspectAsync([new(game, ProviderKind.Steam, "1")], CancellationToken.None);
        var result = await sut.ProtectAsync([new(game, ProviderKind.Steam, "1")], CancellationToken.None);

        Assert.Empty(inventory.Artifacts);
        Assert.Equal(0, result.Protected);
        Assert.Empty(baselines.Items);
        Assert.Empty(snapshots.Items);
    }

    [Fact]
    public async Task Newly_discovered_save_is_protected_without_duplicate_existing_snapshot()
    {
        Directory.CreateDirectory(_root);
        var game = GameId.New();
        var config = Artifact(game, GameLocalArtifactKind.Configuration, "config-new");
        var save = Artifact(game, GameLocalArtifactKind.SaveData, "save-new");
        var discovery = new MutableDiscovery(config);
        var baselines = new MemoryBaselines();
        var snapshots = new MemorySnapshots(_root);
        var sut = new LocalProtectionSetupService(discovery, new Sha256ArtifactFingerprintService(), baselines, snapshots.Service);

        await sut.ProtectAsync([new(game, ProviderKind.Steam, "1")], CancellationToken.None);
        discovery.Artifacts = [config, save];
        var result = await sut.ProtectAsync([new(game, ProviderKind.Steam, "1")], CancellationToken.None);
        var third = await sut.ProtectAsync([new(game, ProviderKind.Steam, "1")], CancellationToken.None);

        Assert.Equal(1, result.Protected);
        Assert.Equal(2, third.AlreadyProtected);
        Assert.Single(snapshots.Items, x => x.ArtifactKind == GameLocalArtifactKind.SaveData);
        Assert.Equal(2, baselines.Items.Count);
    }

    private GameLocalArtifact Artifact(GameId game, GameLocalArtifactKind kind, string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "state.dat"), name);
        return new(game, kind, path, GameLocalArtifactSource.KnownConvention, GameLocalArtifactStatus.KnownAndExists, name);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class FakeDiscovery(params GameLocalArtifact[] artifacts) : IGameLocalArtifactDiscoveryService
    {
        public Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameLocalArtifact>>(artifacts);
        public Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(GameId gameId, ProviderKind? provider, string? providerGameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameLocalArtifact>>(artifacts);
    }

    private sealed class MutableDiscovery(params GameLocalArtifact[] artifacts) : IGameLocalArtifactDiscoveryService
    {
        public IReadOnlyList<GameLocalArtifact> Artifacts { get; set; } = artifacts;
        public Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult(Artifacts);
        public Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(GameId gameId, ProviderKind? provider, string? providerGameId, CancellationToken cancellationToken) => Task.FromResult(Artifacts);
    }

    private sealed class SelectiveDiscovery(params (GameId GameId, IReadOnlyList<GameLocalArtifact> Artifacts)[] entries) : IGameLocalArtifactDiscoveryService
    {
        public Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(GameId gameId, CancellationToken cancellationToken)
            => Task.FromResult(entries.FirstOrDefault(x => x.GameId == gameId).Artifacts ?? []);

        public Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(GameId gameId, ProviderKind? provider, string? providerGameId, CancellationToken cancellationToken)
            => DiscoverAsync(gameId, cancellationToken);
    }

    private sealed class MemoryBaselines : ILocalArtifactBaselineStore
    {
        public Dictionary<(GameId, GameLocalArtifactKind, string), LocalArtifactBaseline> Items { get; } = [];
        public Task<LocalArtifactBaseline?> GetAsync(GameId gameId, GameLocalArtifactKind kind, string artifactIdentity, CancellationToken cancellationToken) => Task.FromResult(Items.TryGetValue((gameId, kind, artifactIdentity), out var value) ? value : null);
        public Task UpsertAsync(LocalArtifactBaseline baseline, CancellationToken cancellationToken) { Items[(baseline.GameId, baseline.Kind, baseline.ArtifactIdentity)] = baseline; return Task.CompletedTask; }
    }

    private sealed class MemorySnapshots
    {
        private readonly Store _store = new();
        public IReadOnlyList<LocalArtifactSnapshot> Items => _store.Items;
        public ILocalArtifactSnapshotService Service { get; } = null!;

        public MemorySnapshots(string root)
        {
            Service = new LocalArtifactSnapshotService(new Sha256ArtifactFingerprintService(), _store, root);
        }

        private sealed class Store : ILocalArtifactSnapshotStore
        {
            public List<LocalArtifactSnapshot> Items { get; } = [];
            public Task<IReadOnlyList<LocalArtifactSnapshot>> GetAsync(GameId gameId, GameLocalArtifactKind kind, string ruleIdentity, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalArtifactSnapshot>>(Items.Where(x => x.GameId == gameId && x.ArtifactKind == kind && x.RuleIdentity == ruleIdentity).ToArray());
            public Task<IReadOnlyList<LocalArtifactSnapshot>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalArtifactSnapshot>>(Items);
            public Task UpsertAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken) { Items.Add(snapshot); return Task.CompletedTask; }
            public Task DeleteAsync(Guid snapshotId, CancellationToken cancellationToken) { Items.RemoveAll(x => x.SnapshotId == snapshotId); return Task.CompletedTask; }
        }
    }
}
