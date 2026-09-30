using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Data.Database;
using PlayStead.Platform.Paths;
using PlayStead.Platform.SingleInstance;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Sessions;
using PlayStead.UI.Steam;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class TaskB3DiscoveryStartupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initial_inventory_is_scheduled_from_durable_library_snapshot()
    {
        Directory.CreateDirectory(_root);
        var install = Path.Combine(_root, "InstalledGame");
        Directory.CreateDirectory(install);
        await File.WriteAllTextAsync(Path.Combine(install, "Game.exe"), "binary");
        var layout = UserDataLayout.FromRoot(_root);
        layout.EnsureDirectoriesExist();
        using var host = PlaySteadHost.Build(layout);
        var services = host.Services;
        var store = services.GetRequiredService<ILibraryStore>();
        await services.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
        var observed = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);
        await store.ApplySourceScanAsync(SourceScanResult.Success(ProviderKind.Steam, observed,
            [DiscoveredInstallation.Create(ProviderKind.Steam, "b3-test-" + Guid.NewGuid().ToString("N"),
                "Installed Game", install, 6, observed)]), CancellationToken.None);
        var durable = await store.LoadSnapshotAsync(CancellationToken.None);
        var installation = Assert.Single(durable.Installations);
        var manager = services.GetRequiredService<DiscoveryInventoryManager>();

        var state = await services.GetRequiredService<LocalStartupPipeline>().InitializeAsync(CancellationToken.None);
        Assert.True(state.Health.IsHealthy);
        Assert.Equal(installation.Id, Assert.Single(state.Snapshot.Installations).Id);
        await manager.AwaitIdleAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var context = manager.GetCurrent(installation.Id);
        Assert.NotNull(context);
        Assert.Contains(context.Inventory.Candidates, candidate =>
            candidate.ExecutablePath.EndsWith("Game.exe", StringComparison.OrdinalIgnoreCase));
        await manager.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Legacy_manual_retail_entry_starts_without_learning_state_conflict()
    {
        Directory.CreateDirectory(_root);
        var gameRoot = Directory.CreateDirectory(Path.Combine(_root, "007 First Light")).FullName;
        var workingDirectory = Directory.CreateDirectory(Path.Combine(gameRoot, "Retail")).FullName;
        var executable = Path.Combine(workingDirectory, "007FirstLight.exe");
        await File.WriteAllTextAsync(executable, "binary");
        var layout = UserDataLayout.FromRoot(_root);
        layout.EnsureDirectoriesExist();
        using var host = PlaySteadHost.Build(layout);
        var services = host.Services;
        await services.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
        var manual = services.GetRequiredService<IManualGameStore>();
        await manual.CreateAsync(
            ManualGameDefinition.Create("007 First Light", executable, workingDirectory, null, workingDirectory),
            CancellationToken.None);

        var state = await services.GetRequiredService<LocalStartupPipeline>()
            .InitializeAsync(CancellationToken.None);

        Assert.True(state.Health.IsHealthy);
        var installation = Assert.Single(state.Snapshot.Installations);
        Assert.Equal(gameRoot, installation.InstallPath);
        Assert.Equal(gameRoot, installation.InstallRootPath);
        var learning = await services.GetRequiredService<IProcessSignatureLearningStore>()
            .LoadAsync(installation.Id, CancellationToken.None);
        Assert.NotNull(learning);
        Assert.Equal(gameRoot, learning!.Inventory.Scope.RootPath);
        await services.GetRequiredService<DiscoveryInventoryManager>()
            .AwaitIdleAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(services.GetRequiredService<DiscoveryInventoryManager>()
            .GetCurrent(installation.Id));
        await services.GetRequiredService<DiscoveryInventoryManager>().StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Legacy_manual_retail_learning_state_is_reconciled_before_startup_guard()
    {
        Directory.CreateDirectory(_root);
        var gameRoot = Directory.CreateDirectory(Path.Combine(_root, "007 First Light")).FullName;
        var workingDirectory = Directory.CreateDirectory(Path.Combine(gameRoot, "Retail")).FullName;
        var executable = Path.Combine(workingDirectory, "007FirstLight.exe");
        await File.WriteAllTextAsync(executable, "binary");
        var layout = UserDataLayout.FromRoot(_root);
        layout.EnsureDirectoriesExist();
        using var host = PlaySteadHost.Build(layout);
        var services = host.Services;
        await services.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
        var manual = services.GetRequiredService<IManualGameStore>();
        var created = await manual.CreateAsync(
            ManualGameDefinition.Create("007 First Light", executable, workingDirectory, null, workingDirectory),
            CancellationToken.None);
        var installation = Assert.Single((await services.GetRequiredService<ILibraryStore>()
            .LoadSnapshotAsync(CancellationToken.None)).Installations);
        var legacyScope = new InstallationScope(created.GameId, installation.Id, workingDirectory,
            Guid.NewGuid(), true);
        var legacyInventory = new ExecutableInventory(legacyScope, InventoryCompleteness.Complete,
            [new ExecutableCandidate(executable, Path.GetFileName(executable),
                new FileRevision(new FileInfo(executable).Length, File.GetLastWriteTimeUtc(executable)))], []);
        var legacyState = new ProcessSignatureLearningState(legacyInventory,
            ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, Guid.NewGuid(), 1, false, null, null, []);
        await using (var connection = new SqliteConnection($"Data Source={layout.DatabasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO process_signature_learning
                    (installation_id, game_id, root_path, generation_id, policy_version,
                     concurrency_token, last_sequence_number, has_ambiguous_installation,
                     inventory_json, reasons_json)
                VALUES ($installation, $game, $root, $generation, $policy, $token,
                        $sequence, $ambiguous, $inventory, $reasons);
                """;
            command.Parameters.AddWithValue("$installation", installation.Id.ToString());
            command.Parameters.AddWithValue("$game", created.GameId.ToString());
            command.Parameters.AddWithValue("$root", workingDirectory);
            command.Parameters.AddWithValue("$generation", legacyScope.GenerationId.ToString("D"));
            command.Parameters.AddWithValue("$policy", legacyState.PolicyVersion);
            command.Parameters.AddWithValue("$token", legacyState.ConcurrencyToken.ToString("N"));
            command.Parameters.AddWithValue("$sequence", legacyState.LastSequenceNumber);
            command.Parameters.AddWithValue("$ambiguous", false);
            command.Parameters.AddWithValue("$inventory", JsonSerializer.Serialize(legacyState.Inventory));
            command.Parameters.AddWithValue("$reasons", JsonSerializer.Serialize(legacyState.Reasons));
            await command.ExecuteNonQueryAsync();
        }

        var state = await services.GetRequiredService<LocalStartupPipeline>()
            .InitializeAsync(CancellationToken.None);

        Assert.True(state.Health.IsHealthy);
        var resolved = Assert.Single(state.Snapshot.Installations);
        Assert.Equal(gameRoot, resolved.InstallPath);
        Assert.Equal(gameRoot, resolved.InstallRootPath);
        var learning = await services.GetRequiredService<IProcessSignatureLearningStore>()
            .LoadAsync(resolved.Id, CancellationToken.None);
        Assert.NotNull(learning);
        Assert.Equal(gameRoot, learning!.Inventory.Scope.RootPath);
        Assert.Equal(0, learning.LastSequenceNumber);
        Assert.Equal(created.GameId, resolved.GameId);
        await services.GetRequiredService<DiscoveryInventoryManager>().StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Stop_joins_background_refresh_before_disposing_host()
    {
        var refreshEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefresh = new TaskCompletionSource<LibrarySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostDisposed = false;
        var snapshot = new LibrarySnapshot([], []);
        var layout = UserDataLayout.FromRoot(_root);
        var operations = new ApplicationStartupCoordinator.Operations(
            _ => new AppInvocation(true, null),
            (_, _) => Task.FromResult(SingleInstanceResult.Primary),
            () => layout,
            _ => { },
            (_, _) => Task.CompletedTask,
            _ => Task.FromResult(new LocalStartupState(new DatabaseHealthResult(true, "ok"), snapshot)),
            _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            _ => { },
            _ => { },
            (_, _) => Task.CompletedTask,
            async _ => { refreshEntered.SetResult(); return await releaseRefresh.Task; },
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => { hostDisposed = true; return Task.CompletedTask; },
            _ => Task.CompletedTask);
        var coordinator = new ApplicationStartupCoordinator(operations);
        Assert.Equal(ApplicationStartupCoordinator.StartResult.Started,
            await coordinator.StartAsync([], CancellationToken.None));
        await refreshEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stop = coordinator.StopAsync(CancellationToken.None);
        Assert.False(hostDisposed);
        Assert.False(stop.IsCompleted);
        releaseRefresh.SetResult(snapshot);
        await stop.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(hostDisposed);
        Assert.True(coordinator.BackgroundRefreshTask!.IsCompleted);
    }

    [Fact]
    public async Task Rescan_hides_old_inventory_before_scan_and_publishes_durable_removal()
    {
        Directory.CreateDirectory(_root);
        var install = Path.Combine(_root, "InstalledGame");
        Directory.CreateDirectory(install);
        await File.WriteAllTextAsync(Path.Combine(install, "Game.exe"), "binary");
        var layout = UserDataLayout.FromRoot(_root);
        layout.EnsureDirectoriesExist();
        using var host = PlaySteadHost.Build(layout);
        var services = host.Services;
        var store = services.GetRequiredService<ILibraryStore>();
        var observed = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);
        await services.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
        await store.ApplySourceScanAsync(SourceScanResult.Success(ProviderKind.Steam, observed,
            [DiscoveredInstallation.Create(ProviderKind.Steam, "b3-rescan", "Installed Game",
                install, 6, observed)]), CancellationToken.None);
        var installation = Assert.Single((await store.LoadSnapshotAsync(CancellationToken.None)).Installations);
        var manager = services.GetRequiredService<DiscoveryInventoryManager>();
        var scan = new PausedSource(observed.AddMinutes(1));
        var pipeline = new LocalStartupPipeline(
            services.GetRequiredService<DatabaseInitializer>(),
            services.GetRequiredService<DatabaseHealthChecker>(), store,
            new LocalScanCoordinator([scan]), new EmptySteamReferenceRuntime(), manager);
        await pipeline.InitializeAsync(CancellationToken.None);
        await manager.AwaitIdleAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(manager.GetCurrent(installation.Id));

        var refresh = pipeline.RefreshAsync(CancellationToken.None);
        await scan.Entered.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Null(manager.GetCurrent(installation.Id));
        Assert.True(installation.IsPresent);
        scan.Release();
        var snapshot = await refresh.WaitAsync(TimeSpan.FromSeconds(5));
        await manager.AwaitIdleAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(Assert.Single(snapshot.Installations).IsPresent);
        Assert.Null(manager.GetCurrent(installation.Id));
        Assert.False(Assert.Single((await store.LoadSnapshotAsync(CancellationToken.None)).Installations).IsPresent);
        await manager.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Faulted_refresh_still_stops_inventory_before_host_and_propagates_failure()
    {
        var order = new List<string>();
        var failed = new TaskCompletionSource<LibrarySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshot = new LibrarySnapshot([], []);
        var layout = UserDataLayout.FromRoot(_root);
        var operations = new ApplicationStartupCoordinator.Operations(
            _ => new AppInvocation(true, null),
            (_, _) => Task.FromResult(SingleInstanceResult.Primary),
            () => layout, _ => { }, (_, _) => Task.CompletedTask,
            _ => Task.FromResult(new LocalStartupState(new DatabaseHealthResult(true, "ok"), snapshot)),
            _ => Task.CompletedTask, (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask, _ => { }, _ => { },
            (_, _) => Task.CompletedTask,
            _ => failed.Task,
            (_, _) => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => { order.Add("host"); return Task.CompletedTask; },
            _ => Task.CompletedTask,
            _ => { order.Add("inventory"); return Task.CompletedTask; });
        var coordinator = new ApplicationStartupCoordinator(operations);
        await coordinator.StartAsync([], CancellationToken.None);
        failed.SetException(new InvalidOperationException("scan failed"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.StopAsync(CancellationToken.None));
        Assert.Equal("scan failed", error.Message);
        Assert.Equal(["inventory", "host"], order);
    }

    [Fact]
    public async Task Faulted_pipe_stop_still_joins_admitted_manual_refresh_before_inventory_and_host()
    {
        var snapshot = new LibrarySnapshot([], []);
        var manualEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseManual = new TaskCompletionSource<LibrarySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<CancellationToken, Task>? rescan = null;
        var refreshCalls = 0;
        var stoppedInventory = false;
        var stoppedHost = false;
        var operations = CreateCoordinatorOperations() with
        {
            BindRescanRequested = handler => rescan = handler,
            RefreshAsync = _ =>
            {
                if (Interlocked.Increment(ref refreshCalls) == 1) return Task.FromResult(snapshot);
                manualEntered.TrySetResult();
                return releaseManual.Task;
            },
            StopPipeAsync = _ =>
            {
                pipeEntered.TrySetResult();
                throw new InvalidOperationException("pipe stop failed");
            },
            StopDiscoveryAsync = _ => { stoppedInventory = true; return Task.CompletedTask; },
            StopHostAsync = _ => { stoppedHost = true; return Task.CompletedTask; }
        };
        var coordinator = new ApplicationStartupCoordinator(operations);
        await coordinator.StartAsync([], CancellationToken.None);
        await coordinator.BackgroundRefreshTask!.WaitAsync(TimeSpan.FromSeconds(2));
        var manual = rescan!(CancellationToken.None);
        await manualEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            var stop = coordinator.StopAsync(CancellationToken.None);
            await pipeEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(stoppedInventory);
            Assert.False(stoppedHost);
            Assert.False(stop.IsCompleted);
            releaseManual.TrySetResult(snapshot);
            await manual.WaitAsync(TimeSpan.FromSeconds(2));
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => stop);
            Assert.Equal("pipe stop failed", error.Message);
            Assert.True(stoppedInventory);
            Assert.True(stoppedHost);
        }
        finally
        {
            releaseManual.TrySetResult(snapshot);
        }
    }

    [Fact]
    public async Task Cached_snapshot_waits_for_initial_inventory_before_returning()
    {
        Directory.CreateDirectory(_root);
        var install = Path.Combine(_root, "InstalledGame");
        Directory.CreateDirectory(install);
        var layout = UserDataLayout.FromRoot(_root);
        layout.EnsureDirectoriesExist();
        using var host = PlaySteadHost.Build(layout);
        var services = host.Services;
        var store = services.GetRequiredService<ILibraryStore>();
        var observed = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);
        await services.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
        await store.ApplySourceScanAsync(SourceScanResult.Success(ProviderKind.Steam, observed,
            [DiscoveredInstallation.Create(ProviderKind.Steam, "b3-cache", "Installed Game",
                install, null, observed)]), CancellationToken.None);
        var source = new BlockedInventorySource();
        var manager = new DiscoveryInventoryManager(source,
            services.GetRequiredService<IProcessSignatureLearningStore>(),
            services.GetRequiredService<ProcessSignatureLearningCoordinator>(),
            services.GetRequiredService<ILogger<DiscoveryInventoryManager>>());
        var pipeline = new LocalStartupPipeline(
            services.GetRequiredService<DatabaseInitializer>(),
            services.GetRequiredService<DatabaseHealthChecker>(), store,
            new LocalScanCoordinator([]), new EmptySteamReferenceRuntime(), manager);
        try
        {
            var initialize = pipeline.InitializeAsync(CancellationToken.None);
            await source.Entered.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(initialize.IsCompleted);
            source.Release();
            var state = await initialize.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(state.Health.IsHealthy);
            Assert.Single(state.Snapshot.Games);
            Assert.NotNull(manager.GetCurrent(Assert.Single(state.Snapshot.Installations).Id));
        }
        finally
        {
            source.Release();
            await manager.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Stop_joins_held_manual_rescan_and_rejects_new_refreshes()
    {
        var snapshot = new LibrarySnapshot([], []);
        var held = new TaskCompletionSource<LibrarySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var manualEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<CancellationToken, Task>? rescan = null;
        var calls = 0;
        var stoppedInventory = false;
        var stoppedHost = false;
        var operations = CreateCoordinatorOperations() with
        {
            BindRescanRequested = handler => rescan = handler,
            RefreshAsync = _ =>
            {
                if (Interlocked.Increment(ref calls) == 1) return Task.FromResult(snapshot);
                manualEntered.TrySetResult();
                return held.Task;
            },
            StopDiscoveryAsync = _ => { stoppedInventory = true; return Task.CompletedTask; },
            StopHostAsync = _ => { stoppedHost = true; return Task.CompletedTask; }
        };
        var coordinator = new ApplicationStartupCoordinator(operations);
        await coordinator.StartAsync([], CancellationToken.None);
        await coordinator.BackgroundRefreshTask!.WaitAsync(TimeSpan.FromSeconds(2));
        var manual = rescan!(CancellationToken.None);
        await manualEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stop = coordinator.StopAsync(CancellationToken.None);
        Assert.False(stoppedInventory);
        Assert.False(stoppedHost);
        Assert.False(stop.IsCompleted);
        var rejectedWhileStopping = rescan!(CancellationToken.None);
        Assert.True(rejectedWhileStopping.IsCanceled);
        Assert.Equal(2, calls);
        held.SetResult(snapshot);
        await manual.WaitAsync(TimeSpan.FromSeconds(2));
        await stop.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(stoppedInventory);
        Assert.True(stoppedHost);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rescan!(CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Canceled_first_stop_can_retry_and_join_inventory_before_host_disposal()
    {
        Directory.CreateDirectory(_root);
        var install = Path.Combine(_root, "InstalledGame");
        Directory.CreateDirectory(install);
        var layout = UserDataLayout.FromRoot(_root);
        layout.EnsureDirectoriesExist();
        using var host = PlaySteadHost.Build(layout);
        var services = host.Services;
        var store = services.GetRequiredService<ILibraryStore>();
        var observed = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);
        await services.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
        await store.ApplySourceScanAsync(SourceScanResult.Success(ProviderKind.Steam, observed,
            [DiscoveredInstallation.Create(ProviderKind.Steam, "b3-stop-retry", "Installed Game",
                install, null, observed)]), CancellationToken.None);
        var source = new StubbornInventorySource();
        var manager = new DiscoveryInventoryManager(source,
            services.GetRequiredService<IProcessSignatureLearningStore>(),
            services.GetRequiredService<ProcessSignatureLearningCoordinator>(),
            services.GetRequiredService<ILogger<DiscoveryInventoryManager>>());
        manager.Schedule(await store.LoadSnapshotAsync(CancellationToken.None), CancellationToken.None);
        await source.Entered.WaitAsync(TimeSpan.FromSeconds(2));
        var hostDisposed = false;
        var operations = CreateCoordinatorOperations() with
        {
            StopDiscoveryAsync = manager.StopAsync,
            StopHostAsync = _ => { hostDisposed = true; return Task.CompletedTask; }
        };
        var coordinator = new ApplicationStartupCoordinator(operations);
        await coordinator.StartAsync([], CancellationToken.None);
        await coordinator.BackgroundRefreshTask!.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => coordinator.StopAsync(canceled.Token));
            Assert.False(hostDisposed);
            source.Release();
            await coordinator.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(hostDisposed);
            await manager.AwaitIdleAsync(CancellationToken.None);
        }
        finally
        {
            source.Release();
            await manager.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Overlapping_rescan_never_republishes_old_inventory_after_newer_request()
    {
        Directory.CreateDirectory(_root);
        var install = Path.Combine(_root, "InstalledGame");
        Directory.CreateDirectory(install);
        await File.WriteAllTextAsync(Path.Combine(install, "Game.exe"), "binary");
        var layout = UserDataLayout.FromRoot(_root);
        layout.EnsureDirectoriesExist();
        using var host = PlaySteadHost.Build(layout);
        var services = host.Services;
        var store = services.GetRequiredService<ILibraryStore>();
        var observed = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);
        await services.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
        var discovered = DiscoveredInstallation.Create(ProviderKind.Steam, "b3-overlap",
            "Installed Game", install, 6, observed);
        await store.ApplySourceScanAsync(SourceScanResult.Success(ProviderKind.Steam, observed,
            [discovered]), CancellationToken.None);
        var installation = Assert.Single((await store.LoadSnapshotAsync(CancellationToken.None)).Installations);
        var manager = services.GetRequiredService<DiscoveryInventoryManager>();
        var source = new OverlappingSource(discovered, observed);
        var pipeline = new LocalStartupPipeline(
            services.GetRequiredService<DatabaseInitializer>(),
            services.GetRequiredService<DatabaseHealthChecker>(), store,
            new LocalScanCoordinator([source]), new EmptySteamReferenceRuntime(), manager);
        await pipeline.InitializeAsync(CancellationToken.None);
        await manager.AwaitIdleAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(manager.GetCurrent(installation.Id));
        Task<LibrarySnapshot>? older = null;
        Task<LibrarySnapshot>? newer = null;
        try
        {
            older = pipeline.RefreshAsync(CancellationToken.None);
            await source.FirstEntered.WaitAsync(TimeSpan.FromSeconds(2));
            newer = pipeline.RefreshAsync(CancellationToken.None);
            Assert.False(source.SecondEntered.IsCompleted);
            source.ReleaseFirst();
            await older.WaitAsync(TimeSpan.FromSeconds(5));
            await manager.AwaitIdleAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(manager.GetCurrent(installation.Id));
            source.ReleaseSecond();
            await source.SecondEntered.WaitAsync(TimeSpan.FromSeconds(2));
            var latest = await newer.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(Assert.Single(latest.Installations).IsPresent);
        }
        finally
        {
            source.ReleaseFirst();
            source.ReleaseSecond();
            if (older is not null) await older;
            if (newer is not null) await newer;
            await manager.StopAsync(CancellationToken.None);
        }
    }

    private ApplicationStartupCoordinator.Operations CreateCoordinatorOperations()
    {
        var snapshot = new LibrarySnapshot([], []);
        return new ApplicationStartupCoordinator.Operations(
            _ => new AppInvocation(true, null),
            (_, _) => Task.FromResult(SingleInstanceResult.Primary),
            () => UserDataLayout.FromRoot(_root), _ => { },
            (_, _) => Task.CompletedTask,
            _ => Task.FromResult(new LocalStartupState(new DatabaseHealthResult(true, "ok"), snapshot)),
            _ => Task.CompletedTask, (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask, _ => { }, _ => { },
            (_, _) => Task.CompletedTask, _ => Task.FromResult(snapshot),
            (_, _) => Task.CompletedTask, _ => Task.CompletedTask,
            _ => Task.CompletedTask, _ => Task.CompletedTask);
    }

    private sealed class StubbornInventorySource : IExecutableInventorySource
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Entered => _entered.Task;
        public void Release() => _release.TrySetResult();
        public async Task<ExecutableInventory> InventoryAsync(InstallationScope scope,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _release.Task;
            return new ExecutableInventory(scope, InventoryCompleteness.Complete, [], []);
        }
    }

    private sealed class OverlappingSource(DiscoveredInstallation discovered,
        DateTimeOffset observed) : ILocalLibrarySource
    {
        private readonly TaskCompletionSource _firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;
        public ProviderKind Provider => ProviderKind.Steam;
        public Task FirstEntered => _firstEntered.Task;
        public Task SecondEntered => _secondEntered.Task;
        public void ReleaseFirst() => _firstRelease.TrySetResult();
        public void ReleaseSecond() => _secondRelease.TrySetResult();
        public async Task<SourceScanResult> ScanAsync(CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call == 1)
            {
                _firstEntered.TrySetResult();
                await _firstRelease.Task.WaitAsync(cancellationToken);
                return SourceScanResult.Success(ProviderKind.Steam, observed.AddMinutes(1),
                    [discovered with { ObservedAtUtc = observed.AddMinutes(1) }]);
            }
            _secondEntered.TrySetResult();
            await _secondRelease.Task.WaitAsync(cancellationToken);
            return SourceScanResult.Success(ProviderKind.Steam, observed.AddMinutes(2), []);
        }
    }

    private sealed class BlockedInventorySource : IExecutableInventorySource
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Entered => _entered.Task;
        public void Release() => _release.TrySetResult();
        public async Task<ExecutableInventory> InventoryAsync(InstallationScope scope,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new ExecutableInventory(scope, InventoryCompleteness.Complete, [], []);
        }
    }

    private sealed class PausedSource(DateTimeOffset observed) : ILocalLibrarySource
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ProviderKind Provider => ProviderKind.Steam;
        public Task Entered => _entered.Task;
        public void Release() => _release.SetResult();
        public async Task<SourceScanResult> ScanAsync(CancellationToken cancellationToken)
        {
            _entered.SetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return SourceScanResult.Success(ProviderKind.Steam, observed, []);
        }
    }

    private sealed class EmptySteamReferenceRuntime : ISteamReferenceRuntime
    {
        public SteamReferenceSnapshot Current { get; } = new([]);
        public Task<SteamReferenceSnapshot> LoadCachedAsync(CancellationToken token) => Task.FromResult(Current);
        public Task<SteamReferenceSnapshot> RefreshStaleAsync(CancellationToken token) => Task.FromResult(Current);
        public Task<SteamReferenceSnapshot> RefreshAllAsync(CancellationToken token) => Task.FromResult(Current);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
