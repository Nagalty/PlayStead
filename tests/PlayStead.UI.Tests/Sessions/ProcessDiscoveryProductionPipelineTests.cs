using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Data.Database;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class ProcessDiscoveryProductionPipelineTests
{
    [Fact]
    public async Task Two_episodes_accept_exact_signature_and_persist_confirmation_as_onboarding_session()
    {
        await using var driver = await Driver.CreateAsync();
        await driver.CompleteEpisodeAsync(100, 1);
        var first = await driver.Learning.LoadAsync(driver.Installation.Id, CancellationToken.None);
        Assert.NotNull(first?.Reference);
        Assert.Null(await driver.Signatures.GetAsync(driver.Installation.GameId.Value, CancellationToken.None));

        await driver.CompleteEpisodeAsync(200, 20);
        var signature = await driver.Signatures.GetAsync(driver.Installation.GameId.Value,
            CancellationToken.None);
        Assert.NotNull(signature);
        Assert.Equal(ProcessSignatureOrigin.Discovered, signature.Origin);
        Assert.Equal(ProcessSignatureValidationState.Valid, signature.Discovery?.ValidationState);
        var main = Assert.Single(signature.Entries);
        Assert.Equal(ProcessSignatureEntryKind.Main, main.Kind);
        Assert.Equal(driver.ExecutablePath, main.ExecutablePath);
        Assert.Equal("Game.exe", main.ExecutableName);
        var consumed = await driver.Learning.LoadAsync(driver.Installation.Id, CancellationToken.None);
        Assert.Null(consumed?.Reference);
        Assert.Null(consumed?.Confirmation);
        var onboarding = Assert.Single(await driver.Sessions.GetRecentAsync(10, CancellationToken.None));
        Assert.Equal(driver.Installation.GameId.Value, onboarding.GameId);
        Assert.Equal(driver.Now(22), onboarding.ObservedStartedAtUtc);
        Assert.Equal(driver.Now(25), onboarding.ObservedEndedAtUtc);
    }

    [Fact]
    public async Task Restart_retains_reference_but_discards_Current()
    {
        await using var driver = await Driver.CreateAsync();
        await driver.CompleteEpisodeAsync(100, 1);
        var before = await driver.Learning.LoadAsync(driver.Installation.Id, CancellationToken.None);
        Assert.NotNull(before?.Reference);
        var referenceId = before.Reference.EpisodeId;
        await driver.TickAsync([], 7);
        await driver.TickAsync([], 8);
        await driver.TickAsync([driver.Process(150, 9)], 9); // interrupted, never ended

        await driver.RestartAsync();
        var after = await driver.Learning.LoadAsync(driver.Installation.Id, CancellationToken.None);
        Assert.Equal(referenceId, after?.Reference?.EpisodeId);
        Assert.Null(after?.Confirmation);
        await driver.TickAsync([driver.Process(150, 9)], 10); // preexisting at restart
        await driver.TickAsync([], 11);
        await driver.TickAsync([], 12);
        Assert.Null(await driver.Signatures.GetAsync(driver.Installation.GameId.Value,
            CancellationToken.None));
        await driver.CompleteEpisodeAsync(200, 20);
        Assert.NotNull(await driver.Signatures.GetAsync(driver.Installation.GameId.Value,
            CancellationToken.None));
    }

    [Fact]
    public async Task After_promotion_next_cycle_only_then_two_snapshots_start_session()
    {
        await using var driver = await Driver.CreateAsync();
        await driver.CompleteEpisodeAsync(100, 1);
        await driver.CompleteEpisodeAsync(200, 20);
        Assert.Empty(await driver.Sessions.GetActiveAsync(CancellationToken.None));
        await driver.TickAsync([driver.Process(300, 30)], 30);
        Assert.Empty(await driver.Sessions.GetActiveAsync(CancellationToken.None));
        await driver.TickAsync([driver.Process(300, 30)], 31);
        var session = Assert.Single(await driver.Sessions.GetActiveAsync(CancellationToken.None));
        Assert.Equal(driver.Installation.GameId.Value, session.GameId);
        Assert.Equal(driver.Now(30), session.ObservedStartedAtUtc);
    }

    [Fact]
    public async Task Heartbeat_and_history_use_only_future_observations()
    {
        await using var driver = await Driver.CreateAsync();
        await driver.CompleteEpisodeAsync(100, 1);
        await driver.CompleteEpisodeAsync(200, 20);
        var onboarding = Assert.Single(await driver.Sessions.GetRecentAsync(10, CancellationToken.None));
        Assert.Equal(driver.Now(22), onboarding.ObservedStartedAtUtc);
        await driver.TickAsync([driver.Process(300, 30)], 30);
        await driver.TickAsync([driver.Process(300, 30)], 31);
        var started = Assert.Single(await driver.Sessions.GetActiveAsync(CancellationToken.None));
        await driver.TickAsync([driver.Process(300, 30)], 32);
        var heartbeat = Assert.Single(await driver.Sessions.GetActiveAsync(CancellationToken.None));
        Assert.Equal(driver.Now(32), heartbeat.LastSeenAtUtc);
        Assert.Equal(driver.Now(30), heartbeat.ObservedStartedAtUtc);
        await driver.TickAsync([], 33);
        await driver.TickAsync([], 34);
        var recent = await driver.Sessions.GetRecentAsync(10, CancellationToken.None);
        Assert.Equal(2, recent.Count);
        var ended = Assert.Single(recent, session =>
            session.ObservedStartedAtUtc == driver.Now(30));
        Assert.Contains(recent, session =>
            session.ObservedStartedAtUtc == driver.Now(22) &&
            session.ObservedEndedAtUtc == driver.Now(25));
        Assert.Equal(started.SessionId, ended.SessionId);
        Assert.Equal(driver.Now(32), ended.LastSeenAtUtc);
        Assert.Equal(driver.Now(32), ended.ObservedEndedAtUtc);
        Assert.True(ended.ObservedStartedAtUtc >= driver.Now(30));
    }

    [Fact]
    public async Task Ambiguous_installation_refuses_without_signature()
    {
        await using var driver = await Driver.CreateAsync();
        await driver.AddNestedInstallationAsync();
        Assert.True(driver.Manager.GetCurrent(driver.Installation.Id)?.HasAmbiguousInstallation);
        await driver.CompleteEpisodeAsync(100, 1);
        await driver.CompleteEpisodeAsync(200, 20);
        Assert.Null(await driver.Signatures.GetAsync(driver.Installation.GameId.Value,
            CancellationToken.None));
    }

    [Fact]
    public async Task Revision_change_suspends_discovered_no_name_fallback()
    {
        await using var driver = await Driver.CreateAsync();
        await driver.CompleteEpisodeAsync(100, 1);
        await driver.CompleteEpisodeAsync(200, 20);
        var accepted = await driver.Signatures.GetAsync(driver.Installation.GameId.Value,
            CancellationToken.None);
        Assert.Equal(ProcessSignatureValidationState.Valid, accepted?.Discovery?.ValidationState);
        await File.AppendAllTextAsync(driver.ExecutablePath, " changed revision");
        await driver.TickAsync([new ProcessSnapshot(300, "Game.exe", null, driver.Now(30))], 30);
        await driver.TickAsync([new ProcessSnapshot(300, "Game.exe", null, driver.Now(30))], 31);
        var suspended = await driver.Signatures.GetAsync(driver.Installation.GameId.Value,
            CancellationToken.None);
        Assert.Equal(ProcessSignatureValidationState.NeedsRevalidation,
            suspended?.Discovery?.ValidationState);
        Assert.Empty(await driver.Sessions.GetActiveAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(ProcessSignatureOrigin.Manual)]
    [InlineData(ProcessSignatureOrigin.BuiltIn)]
    public async Task Manual_and_BuiltIn_are_untouched(ProcessSignatureOrigin origin)
    {
        await using var driver = await Driver.CreateAsync();
        await driver.Signatures.UpsertAsync(new ProcessSignature(driver.Installation.GameId.Value,
            [new ProcessSignatureEntry("Game.exe", ProcessSignatureEntryKind.Main)], origin,
            driver.Now(0)), CancellationToken.None);
        await driver.CompleteEpisodeAsync(100, 1);
        await driver.CompleteEpisodeAsync(200, 20);
        var protectedSignature = await driver.Signatures.GetAsync(driver.Installation.GameId.Value,
            CancellationToken.None);
        Assert.Equal(origin, protectedSignature?.Origin);
        Assert.Null(protectedSignature?.Discovery);
        await driver.TickAsync([driver.Process(300, 30)], 30);
        await driver.TickAsync([driver.Process(300, 30)], 31);
        Assert.NotEmpty(await driver.Sessions.GetActiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_manual_write_discards_inflight_discovery_without_overwrite()
    {
        await using var driver = await Driver.CreateAsync();
        await driver.TickAsync([], 1);
        await driver.TickAsync([], 2);
        await driver.TickAsync([driver.Process(100, 3)], 3);
        await driver.TickAsync([driver.Process(100, 3)], 4);
        await driver.Signatures.UpsertAsync(new ProcessSignature(driver.Installation.GameId.Value,
            [new ProcessSignatureEntry("Game.exe", ProcessSignatureEntryKind.Main)],
            ProcessSignatureOrigin.Manual, driver.Now(4)), CancellationToken.None);
        await driver.TickAsync([], 5);
        await driver.TickAsync([], 6);
        var current = await driver.Signatures.GetAsync(driver.Installation.GameId.Value,
            CancellationToken.None);
        Assert.Equal(ProcessSignatureOrigin.Manual, current?.Origin);
        Assert.Null((await driver.Learning.LoadAsync(driver.Installation.Id,
            CancellationToken.None))?.Reference);
    }

    [Fact]
    public async Task Concurrent_new_generation_cannot_be_overwritten_by_stale_episode()
    {
        await using var driver = await Driver.CreateAsync();
        await driver.TickAsync([], 1);
        await driver.TickAsync([], 2);
        await driver.TickAsync([driver.Process(100, 3)], 3);
        await driver.TickAsync([driver.Process(100, 3)], 4);
        var old = await driver.Learning.LoadAsync(driver.Installation.Id, CancellationToken.None);
        Assert.NotNull(old);
        var newGeneration = Guid.NewGuid();
        var scope = new InstallationScope(old.Inventory.Scope.GameId,
            old.Inventory.Scope.InstallationId, old.Inventory.Scope.RootPath,
            newGeneration, old.Inventory.Scope.IsPresent);
        var inventory = new ExecutableInventory(scope, old.Inventory.Completeness,
            old.Inventory.Candidates, old.Inventory.Issues);
        var replacement = new ProcessSignatureLearningState(inventory, old.PolicyVersion,
            Guid.NewGuid(), old.LastSequenceNumber, old.HasAmbiguousInstallation,
            null, null, []);
        Assert.True(await driver.Learning.TrySaveAsync(replacement,
            old.ConcurrencyToken, CancellationToken.None));
        await driver.TickAsync([], 5);
        await driver.TickAsync([], 6);
        await driver.Manager.AwaitIdleAsync(CancellationToken.None);
        var durable = await driver.Learning.LoadAsync(driver.Installation.Id, CancellationToken.None);
        Assert.Equal(newGeneration, durable?.Inventory.Scope.GenerationId);
        Assert.Null(durable?.Reference);
        Assert.Null(await driver.Signatures.GetAsync(driver.Installation.GameId.Value,
            CancellationToken.None));
    }

    [Fact]
    public async Task No_second_capture_or_DB_write_per_stable_tick()
    {
        await using var driver = await Driver.CreateAsync();
        await driver.TickAsync([], 1);
        await driver.TickAsync([], 2);
        await driver.TickAsync([driver.Process(100, 3)], 3);
        var before = await driver.Learning.LoadAsync(driver.Installation.Id, CancellationToken.None);
        var captures = driver.CaptureCalls;
        var inventory = driver.Manager.GetCurrent(driver.Installation.Id)?.Inventory.Scope.GenerationId;
        await driver.TickAsync([driver.Process(100, 3)], 4);
        await driver.TickAsync([driver.Process(100, 3)], 5);
        var after = await driver.Learning.LoadAsync(driver.Installation.Id, CancellationToken.None);
        Assert.Equal(captures + 2, driver.CaptureCalls);
        Assert.Equal(before?.ConcurrencyToken, after?.ConcurrencyToken);
        Assert.Equal(inventory, driver.Manager.GetCurrent(driver.Installation.Id)?.Inventory.Scope.GenerationId);
    }

    [Fact]
    public async Task Historical_session_requires_page_reopen_to_refresh_current_UI()
    {
        await using var driver = await Driver.CreateAsync();
        var viewModel = new SessionViewModel(driver.Library, driver.Monitor,
            TimeProvider.System, driver.Sessions, driver.Corrections, driver.Runtime,
            new SessionCorrectionPolicy());
        await viewModel.RefreshAsync(CancellationToken.None); // page-load equivalent
        Assert.Empty(viewModel.RecentSessions);
        await driver.CompleteEpisodeAsync(100, 1);
        await driver.CompleteEpisodeAsync(200, 20);
        await driver.TickAsync([driver.Process(300, 30)], 30);
        await driver.TickAsync([driver.Process(300, 30)], 31);
        await driver.TickAsync([], 32);
        await driver.TickAsync([], 33);
        Assert.Equal(2, (await driver.Sessions.GetRecentAsync(10, CancellationToken.None)).Count);
        Assert.Empty(viewModel.RecentSessions); // live timer alone does not reload history
        await viewModel.RefreshAsync(CancellationToken.None); // reopen page
        Assert.Equal(2, viewModel.RecentSessions.Count);
    }

    private sealed class Driver : IAsyncDisposable
    {
        private static readonly DateTimeOffset Epoch = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);
        private readonly string _root;
        private Microsoft.Extensions.Hosting.IHost _host;
        private readonly ProcessSource _source = new();
        private readonly Clock _clock = new();
        private SessionRuntime _runtime;
        private DiscoveryInventoryManager _manager;

        private Driver(string root, Microsoft.Extensions.Hosting.IHost host,
            GameInstallation installation, string executablePath)
        {
            _root = root;
            _host = host;
            Installation = installation;
            ExecutablePath = executablePath;
            var services = host.Services;
            Signatures = services.GetRequiredService<IProcessSignatureStore>();
            Learning = services.GetRequiredService<IProcessSignatureLearningStore>();
            Sessions = services.GetRequiredService<ISessionStore>();
            _manager = services.GetRequiredService<DiscoveryInventoryManager>();
            _runtime = SessionRuntime.CreateWithObserver(_source, Signatures, Sessions,
                services.GetRequiredService<ProcessSignatureMatcher>(),
                services.GetRequiredService<SessionTransitionPolicy>(),
                services.GetRequiredService<ISessionCorrectionStore>(),
                services.GetRequiredService<SessionCorrectionPolicy>(), _clock,
                services.GetRequiredService<IProcessCaptureObserver>(),
                services.GetRequiredService<IDiscoveredSignatureValidator>());
        }

        public GameInstallation Installation { get; }
        public string ExecutablePath { get; }
        public IProcessSignatureStore Signatures { get; private set; }
        public IProcessSignatureLearningStore Learning { get; private set; }
        public ISessionStore Sessions { get; private set; }
        public ILibraryStore Library => _host.Services.GetRequiredService<ILibraryStore>();
        public ISessionCorrectionStore Corrections =>
            _host.Services.GetRequiredService<ISessionCorrectionStore>();
        public SessionMonitor Monitor => _host.Services.GetRequiredService<SessionMonitor>();
        public SessionRuntime Runtime => _runtime;
        public DiscoveryInventoryManager Manager => _manager;
        public int CaptureCalls => _source.CaptureCalls;
        public DateTimeOffset Now(int minute) => Epoch.AddMinutes(minute);

        public static async Task<Driver> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));
            var installRoot = Path.Combine(root, "InstalledGame");
            Directory.CreateDirectory(installRoot);
            var executable = Path.Combine(installRoot, "Game.exe");
            await File.WriteAllTextAsync(executable, "real local fixture binary");
            var layout = UserDataLayout.FromRoot(root);
            layout.EnsureDirectoriesExist();
            var host = PlaySteadHost.Build(layout);
            try
            {
                var services = host.Services;
                await services.GetRequiredService<DatabaseInitializer>()
                    .InitializeAsync(CancellationToken.None);
                var store = services.GetRequiredService<ILibraryStore>();
                await store.ApplySourceScanAsync(SourceScanResult.Success(ProviderKind.Steam, Epoch,
                    [DiscoveredInstallation.Create(ProviderKind.Steam,
                        "pipeline-" + Guid.NewGuid().ToString("N"), "Installed Game", installRoot,
                        new FileInfo(executable).Length, Epoch)]), CancellationToken.None);
                var snapshot = await store.LoadSnapshotAsync(CancellationToken.None);
                var installation = Assert.Single(snapshot.Installations);
                var driver = new Driver(root, host, installation, executable);
                driver._manager.Schedule(snapshot, CancellationToken.None);
                await driver._manager.AwaitIdleAsync(CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(10));
                var inventory = driver._manager.GetCurrent(installation.Id);
                Assert.NotNull(inventory);
                Assert.Equal(InventoryCompleteness.Complete, inventory.Inventory.Completeness);
                Assert.Equal(executable, Assert.Single(inventory.Inventory.Candidates).ExecutablePath);
                return driver;
            }
            catch
            {
                host.Dispose();
                SqliteConnection.ClearAllPools();
                Directory.Delete(root, recursive: true);
                throw;
            }
        }

        public async Task TickAsync(IReadOnlyList<ProcessSnapshot> processes, int minute)
        {
            _clock.Now = Epoch.AddMinutes(minute);
            _source.Next = new ProcessCaptureResult(processes, true);
            await _runtime.RefreshAsync(CancellationToken.None);
        }

        public async Task RestartAsync()
        {
            await _manager.StopAsync(CancellationToken.None);
            _host.Dispose();
            _host = PlaySteadHost.Build(UserDataLayout.FromRoot(_root));
            var services = _host.Services;
            Signatures = services.GetRequiredService<IProcessSignatureStore>();
            Learning = services.GetRequiredService<IProcessSignatureLearningStore>();
            Sessions = services.GetRequiredService<ISessionStore>();
            _manager = services.GetRequiredService<DiscoveryInventoryManager>();
            _manager.Schedule(await Library.LoadSnapshotAsync(CancellationToken.None),
                CancellationToken.None);
            await _manager.AwaitIdleAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10));
            _runtime = SessionRuntime.CreateWithObserver(_source, Signatures, Sessions,
                services.GetRequiredService<ProcessSignatureMatcher>(),
                services.GetRequiredService<SessionTransitionPolicy>(),
                services.GetRequiredService<ISessionCorrectionStore>(),
                services.GetRequiredService<SessionCorrectionPolicy>(), _clock,
                services.GetRequiredService<IProcessCaptureObserver>(),
                services.GetRequiredService<IDiscoveredSignatureValidator>());
        }

        public async Task AddNestedInstallationAsync()
        {
            var nested = Path.Combine(Installation.InstallPath, "Nested");
            Directory.CreateDirectory(nested);
            await File.WriteAllTextAsync(Path.Combine(nested, "Other.exe"), "other local binary");
            await Library.ApplySourceScanAsync(SourceScanResult.Success(ProviderKind.Steam, Epoch,
                [DiscoveredInstallation.Create(ProviderKind.Steam, Installation.ExternalId,
                    "Installed Game", Installation.InstallPath, new FileInfo(ExecutablePath).Length, Epoch),
                 DiscoveredInstallation.Create(ProviderKind.Steam,
                    "nested-" + Guid.NewGuid().ToString("N"), "Nested", nested, 18, Epoch)]),
                CancellationToken.None);
            _manager.MarkRefreshing();
            _manager.Schedule(await Library.LoadSnapshotAsync(CancellationToken.None),
                CancellationToken.None);
            await _manager.AwaitIdleAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10));
        }

        public ProcessSnapshot Process(int pid, int minute) =>
            new(pid, "Game.exe", ExecutablePath, Epoch.AddMinutes(minute));

        public async Task CompleteEpisodeAsync(int pid, int firstMinute)
        {
            await TickAsync([], firstMinute);
            await TickAsync([], firstMinute + 1);
            await TickAsync([Process(pid, firstMinute + 2)], firstMinute + 2);
            await TickAsync([Process(pid, firstMinute + 2)], firstMinute + 3);
            await TickAsync([], firstMinute + 4);
            await TickAsync([], firstMinute + 5);
            await _manager.AwaitIdleAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10));
        }

        public async ValueTask DisposeAsync()
        {
            await _manager.StopAsync(CancellationToken.None);
            _host.Dispose();
            SqliteConnection.ClearAllPools();
            Directory.Delete(_root, recursive: true);
        }

        private sealed class Clock : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = Epoch;
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private sealed class ProcessSource : IProcessSnapshotSource
        {
            public ProcessCaptureResult Next { get; set; } = new([], true);
            public int CaptureCalls { get; private set; }
            public Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(CancellationToken token) =>
                Task.FromResult(Next.Processes);
            public Task<ProcessCaptureResult> CaptureWithQualityAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                CaptureCalls++;
                return Task.FromResult(Next);
            }
        }
    }
}
