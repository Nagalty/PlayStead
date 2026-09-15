using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Platform.Processes.Discovery;

namespace PlayStead.UI.Sessions;

/// <summary>
/// Owns inventory work after a durable Library snapshot. Published contexts always
/// describe one completed snapshot; pending work is deliberately invisible.
/// </summary>
public sealed class DiscoveryInventoryManager
{
    private readonly IExecutableInventorySource _inventorySource;
    private readonly IProcessSignatureLearningStore _learningStore;
    private readonly ProcessSignatureLearningCoordinator _coordinator;
    private readonly ILogger<DiscoveryInventoryManager> _logger;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<Task> _work = [];
    private readonly HashSet<InstallationId> _removed = [];
    private ImmutableDictionary<InstallationId, DiscoveryInventoryContext> _published =
        ImmutableDictionary<InstallationId, DiscoveryInventoryContext>.Empty;
    private ImmutableDictionary<InstallationId, PreparedInstallation> _active =
        ImmutableDictionary<InstallationId, PreparedInstallation>.Empty;
    private CancellationTokenSource? _currentWork;
    private long _revision;
    private bool _stopped;

    public DiscoveryInventoryManager(IExecutableInventorySource inventorySource,
        IProcessSignatureLearningStore learningStore,
        ProcessSignatureLearningCoordinator coordinator,
        ILogger<DiscoveryInventoryManager> logger)
    {
        _inventorySource = inventorySource ?? throw new ArgumentNullException(nameof(inventorySource));
        _learningStore = learningStore ?? throw new ArgumentNullException(nameof(learningStore));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public DiscoveryInventoryContext? GetCurrent(InstallationId installationId)
    {
        lock (_gate) return _published.GetValueOrDefault(installationId);
    }

    public void MarkRefreshing()
    {
        lock (_gate)
        {
            if (_stopped) return;
            _revision++;
            _currentWork?.Cancel();
            _published = ImmutableDictionary<InstallationId, DiscoveryInventoryContext>.Empty;
        }
    }

    public void Schedule(LibrarySnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = PrepareSnapshot(snapshot);
        lock (_gate)
        {
            if (_stopped) return;
            foreach (var id in _active.Keys.Except(prepared.Keys))
                _removed.Add(id);
            foreach (var installation in snapshot.Installations.Where(item => !item.IsPresent))
                _removed.Add(installation.Id);
            foreach (var installation in prepared.Values.Where(item => item.Root is null))
                _removed.Add(installation.Installation.Id);
            _currentWork?.Cancel();
            var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetime.Token);
            _currentWork = workCancellation;
            var revision = ++_revision;
            _active = prepared;
            _published = ImmutableDictionary<InstallationId, DiscoveryInventoryContext>.Empty;
            StartWork(() => InventorySnapshotAsync(prepared, revision, workCancellation.Token),
                workCancellation);
        }
    }

    public void RequestEpisodePreparation(InstallationId installationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_stopped || !_active.TryGetValue(installationId, out var installation)) return;
            _currentWork?.Cancel();
            var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetime.Token);
            _currentWork = workCancellation;
            var revision = ++_revision;
            _published = _published.Remove(installationId);
            StartWork(() => PrepareEpisodeAsync(installation, revision, workCancellation.Token),
                workCancellation);
        }
    }

    public async Task AwaitIdleAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task[] running;
            lock (_gate) running = _work.Where(task => !task.IsCompleted).ToArray();
            if (running.Length == 0) return;
            await Task.WhenAll(running).WaitAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
            _revision++;
            _lifetime.Cancel();
            _currentWork?.Cancel();
            _active = ImmutableDictionary<InstallationId, PreparedInstallation>.Empty;
            _published = ImmutableDictionary<InstallationId, DiscoveryInventoryContext>.Empty;
        }
        await AwaitIdleAsync(cancellationToken);
        _lifetime.Dispose();
    }

    private ImmutableDictionary<InstallationId, PreparedInstallation> PrepareSnapshot(
        LibrarySnapshot snapshot)
    {
        var present = snapshot.Installations.Where(item => item.IsPresent).ToArray();
        var builder = ImmutableDictionary.CreateBuilder<InstallationId, PreparedInstallation>();
        foreach (var installation in present)
        {
            string? root = null;
            try { root = WindowsExecutablePath.NormalizeRoot(installation.InstallPath); }
            catch (ArgumentException)
            {
                _logger.LogWarning("Discovery inventory root invalid for installation {InstallationId}",
                    installation.Id);
            }
            builder[installation.Id] = new PreparedInstallation(installation, root, false);
        }
        var result = builder.ToImmutable();
        foreach (var item in result.Values)
        {
            if (item.Root is null) continue;
            var ambiguous = result.Values.Any(other => other.Installation.Id != item.Installation.Id &&
                other.Root is not null && (string.Equals(item.Root, other.Root,
                    StringComparison.OrdinalIgnoreCase) ||
                    WindowsExecutablePath.IsStrictlyUnderRoot(item.Root, other.Root) ||
                    WindowsExecutablePath.IsStrictlyUnderRoot(other.Root, item.Root)));
            if (ambiguous)
                result = result.SetItem(item.Installation.Id, item with { Ambiguous = true });
        }
        return result;
    }

    private void StartWork(Func<Task> operation, CancellationTokenSource cancellation)
    {
        // Task.Run is required: the Platform inventory performs its recursive walk
        // synchronously before returning Task.FromResult.
        var work = Task.Run(operation, CancellationToken.None);
        _work.Add(work);
        _ = work.ContinueWith(completed =>
        {
            lock (_gate)
            {
                _work.Remove(completed);
                if (ReferenceEquals(_currentWork, cancellation)) _currentWork = null;
            }
            cancellation.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task InventorySnapshotAsync(
        ImmutableDictionary<InstallationId, PreparedInstallation> installations,
        long revision, CancellationToken cancellationToken)
    {
        var completed = ImmutableDictionary.CreateBuilder<InstallationId, DiscoveryInventoryContext>();
        foreach (var installation in installations.Values)
        {
            if (!IsCurrent(revision, cancellationToken)) return;
            var context = await InventoryOneAsync(installation, revision, cancellationToken,
                prepareEpisode: false);
            if (context is not null) completed[installation.Installation.Id] = context;
        }
        lock (_gate)
            if (IsCurrentLocked(revision, cancellationToken))
                _published = completed.ToImmutable();
    }

    private async Task PrepareEpisodeAsync(PreparedInstallation installation,
        long revision, CancellationToken cancellationToken)
    {
        var context = await InventoryOneAsync(installation, revision, cancellationToken,
            prepareEpisode: true);
        if (context is null) return;
        lock (_gate)
            if (IsCurrentLocked(revision, cancellationToken))
                _published = _published.SetItem(installation.Installation.Id, context);
    }

    private async Task<DiscoveryInventoryContext?> InventoryOneAsync(
        PreparedInstallation installation, long revision, CancellationToken cancellationToken,
        bool prepareEpisode)
    {
        var id = installation.Installation.Id;
        if (installation.Root is null) return null;
        try
        {
            var persisted = await _learningStore.LoadAsync(id, cancellationToken);
            if (!IsCurrent(revision, cancellationToken)) return null;
            bool reappeared;
            lock (_gate) reappeared = _removed.Contains(id);
            var generation = !reappeared && persisted is not null &&
                persisted.Inventory.Scope.IsPresent &&
                string.Equals(persisted.Inventory.Scope.RootPath, installation.Root,
                    StringComparison.OrdinalIgnoreCase)
                ? persisted.Inventory.Scope.GenerationId : Guid.NewGuid();
            var scope = new InstallationScope(installation.Installation.GameId, id,
                installation.Root, generation, true);
            ExecutableInventory inventory;
            try
            {
                inventory = await _inventorySource.InventoryAsync(scope, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                if (!IsCurrent(revision, cancellationToken)) return null;
                _logger.LogWarning(error,
                    "Discovery inventory failed for installation {InstallationId}", id);
                inventory = new ExecutableInventory(scope, InventoryCompleteness.Incomplete, [],
                    [new InventoryIssue(scope.RootPath, InventoryIssueKind.IoFailure)]);
            }
            if (!IsCurrent(revision, cancellationToken)) return null;
            var incoming = new DiscoveryInventoryContext(inventory, installation.Ambiguous);
            ProcessSignatureLearningState state;
            if (prepareEpisode)
            {
                var token = _coordinator.GetState(id)?.ConcurrencyToken ??
                    persisted?.ConcurrencyToken;
                if (token is null) return null;
                if (!await _coordinator.PrepareEpisodeAsync(incoming, token.Value,
                    cancellationToken)) return null;
                state = _coordinator.GetState(id)!;
            }
            else state = await _coordinator.InitializeAsync(incoming, cancellationToken);
            lock (_gate)
            {
                if (!IsCurrentLocked(revision, cancellationToken)) return null;
                _removed.Remove(id);
            }
            if (state.Inventory.Completeness != InventoryCompleteness.Complete)
                return null;
            return new DiscoveryInventoryContext(state.Inventory, state.HasAmbiguousInstallation);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _logger.LogWarning(error,
                "Discovery inventory remains pending for installation {InstallationId}", id);
            return null;
        }
    }

    private bool IsCurrent(long revision, CancellationToken cancellationToken)
    {
        lock (_gate) return IsCurrentLocked(revision, cancellationToken);
    }

    private bool IsCurrentLocked(long revision, CancellationToken cancellationToken) =>
        !_stopped && !cancellationToken.IsCancellationRequested && _revision == revision;

    private sealed record PreparedInstallation(GameInstallation Installation,
        string? Root, bool Ambiguous);
}
