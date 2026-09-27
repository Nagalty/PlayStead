using System.Collections.Immutable;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
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
    private readonly List<ExceptionDispatchInfo> _unexpectedFaults = [];
    private readonly HashSet<InstallationId> _removed = [];
    private readonly HashSet<InstallationId> _faulted = [];
    private readonly Dictionary<InstallationId, ScopeRequest> _scopeWork = [];
    private readonly Dictionary<InstallationId, long> _scopeRevisions = [];
    private readonly Dictionary<InstallationId, (Guid Generation, string Reason)> _loggedFailures = [];
    private ImmutableDictionary<InstallationId, DiscoveryInventoryContext> _published =
        ImmutableDictionary<InstallationId, DiscoveryInventoryContext>.Empty;
    private ImmutableDictionary<InstallationId, PreparedInstallation> _active =
        ImmutableDictionary<InstallationId, PreparedInstallation>.Empty;
    private CancellationTokenSource? _currentWork;
    private long _revision;
    private bool _fullRunning;
    private bool _stopped;
    private bool _joined;

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

    public IReadOnlyList<DiscoveryInventoryContext> GetCurrentContexts()
    {
        lock (_gate) return Array.AsReadOnly(_published.Values.ToArray());
    }

    public bool ContainsExecutableName(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName)) return false;
        lock (_gate)
            return _published.Values.Any(context =>
                context.Inventory.Candidates.Any(candidate =>
                    string.Equals(candidate.ExecutableName, executableName,
                        StringComparison.OrdinalIgnoreCase)));
    }

    public void MarkRefreshing()
    {
        lock (_gate)
        {
            if (_stopped) return;
            _revision++;
            _currentWork?.Cancel();
            CancelScopeWork();
            _fullRunning = false;
            _published = ImmutableDictionary<InstallationId, DiscoveryInventoryContext>.Empty;
        }
    }

    public bool MarkPending(InstallationId installationId,
        DiscoveryInventoryContext expectedContext)
    {
        ArgumentNullException.ThrowIfNull(expectedContext);
        lock (_gate)
        {
            if (_stopped || !ReferenceEquals(_published.GetValueOrDefault(installationId),
                    expectedContext)) return false;
            _faulted.Add(installationId);
            _published = _published.Remove(installationId);
            if (_scopeWork.Remove(installationId, out var request))
            {
                request.Cancellation.Cancel();
                if (request.Queued) request.Cancellation.Dispose();
            }
            _scopeRevisions[installationId] =
                _scopeRevisions.GetValueOrDefault(installationId) + 1;
            return true;
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
            CancelScopeWork();
            var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetime.Token);
            _currentWork = workCancellation;
            var revision = ++_revision;
            _fullRunning = true;
            _active = prepared;
            _faulted.Clear();
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
            if (_scopeWork.Remove(installationId, out var prior))
            {
                prior.Cancellation.Cancel();
                if (prior.Queued) prior.Cancellation.Dispose();
            }
            var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetime.Token);
            var scopeRevision = _scopeRevisions.GetValueOrDefault(installationId) + 1;
            _scopeRevisions[installationId] = scopeRevision;
            var request = new ScopeRequest(installation, workCancellation, scopeRevision,
                _fullRunning);
            _scopeWork[installationId] = request;
            _published = _published.Remove(installationId);
            if (!_fullRunning) StartScopeWork(request, _revision);
        }
    }

    public async Task AwaitIdleAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task[] running;
            ExceptionDispatchInfo? fault;
            lock (_gate)
            {
                running = _work.ToArray();
                fault = running.Length == 0 ? _unexpectedFaults.FirstOrDefault() : null;
            }
            if (running.Length == 0)
            {
                fault?.Throw();
                return;
            }
            // Join all owners even if one faulted; its original failure is retained
            // and thrown once the work set has drained.
            await Task.WhenAll(running.Select(task => task.ContinueWith(
                static _ => { }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)))
                .WaitAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_joined)
            {
                _unexpectedFaults.FirstOrDefault()?.Throw();
                return;
            }
            if (!_stopped)
            {
                _stopped = true;
                _revision++;
                _lifetime.Cancel();
                _currentWork?.Cancel();
                CancelScopeWork();
                _fullRunning = false;
                _active = ImmutableDictionary<InstallationId, PreparedInstallation>.Empty;
                _published = ImmutableDictionary<InstallationId, DiscoveryInventoryContext>.Empty;
            }
        }
        try { await AwaitIdleAsync(cancellationToken); }
        finally
        {
            lock (_gate)
            {
                if (!_joined && _work.Count == 0)
                {
                    _joined = true;
                    _lifetime.Dispose();
                }
            }
        }
    }

    private ImmutableDictionary<InstallationId, PreparedInstallation> PrepareSnapshot(
        LibrarySnapshot snapshot)
    {
        var present = snapshot.Installations
            .Where(item => item.IsPresent && item.ContentKind.IsGameEligible())
            .ToArray();
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

    private void CancelScopeWork()
    {
        foreach (var request in _scopeWork.Values)
        {
            request.Cancellation.Cancel();
            if (request.Queued) request.Cancellation.Dispose();
        }
        _scopeWork.Clear();
    }

    private void StartScopeWork(ScopeRequest request, long snapshotRevision)
    {
        request.Queued = false;
        StartWork(() => PrepareEpisodeAsync(request, snapshotRevision),
            request.Cancellation, request.Installation.Installation.Id);
    }

    private void StartWork(Func<Task> operation, CancellationTokenSource cancellation,
        InstallationId? scopeId = null)
    {
        // Task.Run is required: the Platform inventory performs its recursive walk
        // synchronously before returning Task.FromResult.
        var work = Task.Run(operation, CancellationToken.None);
        _work.Add(work);
        _ = work.ContinueWith(completed =>
        {
            Exception? unexpected = completed.Exception?.InnerException;
            lock (_gate)
            {
                if (unexpected is not null)
                    _unexpectedFaults.Add(ExceptionDispatchInfo.Capture(unexpected));
                _work.Remove(completed);
                if (scopeId is { } id)
                {
                    if (_scopeWork.TryGetValue(id, out var request) &&
                        ReferenceEquals(request.Cancellation, cancellation))
                        _scopeWork.Remove(id);
                }
                else if (ReferenceEquals(_currentWork, cancellation)) _currentWork = null;
            }
            if (unexpected is not null)
                _logger.LogError(unexpected,
                    "Discovery unexpected inventory worker fault for installation {InstallationId}",
                    scopeId);
            cancellation.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task InventorySnapshotAsync(
        ImmutableDictionary<InstallationId, PreparedInstallation> installations,
        long revision, CancellationToken cancellationToken)
    {
        try
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
            {
                if (!IsCurrentLocked(revision, cancellationToken)) return;
                _fullRunning = false;
                foreach (var id in _scopeWork.Keys)
                    completed.Remove(id);
                foreach (var id in _faulted)
                    completed.Remove(id);
                _published = completed.ToImmutable();
                foreach (var request in _scopeWork.Values.Where(item => item.Queued).ToArray())
                    StartScopeWork(request, revision);
            }
        }
        finally
        {
            lock (_gate)
            {
                if (_revision == revision && _fullRunning)
                {
                    _fullRunning = false;
                    foreach (var request in _scopeWork.Values.Where(item => item.Queued).ToArray())
                        StartScopeWork(request, revision);
                }
            }
        }
    }

    private async Task PrepareEpisodeAsync(ScopeRequest request, long revision)
    {
        var id = request.Installation.Installation.Id;
        var cancellationToken = request.Cancellation.Token;
        var context = await InventoryOneAsync(request.Installation, revision, cancellationToken,
            prepareEpisode: true, id, request.Revision);
        if (context is null) return;
        lock (_gate)
            if (IsCurrentLocked(revision, cancellationToken, id, request.Revision) &&
                !_faulted.Contains(id))
                _published = _published.SetItem(id, context);
    }

    private async Task<DiscoveryInventoryContext?> InventoryOneAsync(
        PreparedInstallation installation, long revision, CancellationToken cancellationToken,
        bool prepareEpisode, InstallationId? requestId = null, long scopeRevision = 0)
    {
        var total = Stopwatch.StartNew();
        var id = installation.Installation.Id;
        if (installation.Root is null) return null;
        var knownGeneration = Guid.Empty;
        try
        {
            var persistence = Stopwatch.StartNew();
            var persisted = await _learningStore.LoadAsync(id, cancellationToken);
            var persistenceMilliseconds = persistence.ElapsedMilliseconds;
            if (!IsCurrent(revision, cancellationToken, requestId, scopeRevision)) return null;
            bool reappeared;
            lock (_gate) reappeared = _removed.Contains(id);
            var generation = !reappeared && persisted is not null &&
                persisted.Inventory.Scope.IsPresent &&
                string.Equals(persisted.Inventory.Scope.RootPath, installation.Root,
                    StringComparison.OrdinalIgnoreCase)
                ? persisted.Inventory.Scope.GenerationId : Guid.NewGuid();
            knownGeneration = generation;
            var scope = new InstallationScope(installation.Installation.GameId, id,
                installation.Root, generation, true);
            ExecutableInventory inventory;
            long inventoryMilliseconds = 0;
            try
            {
                var inventoryStopwatch = Stopwatch.StartNew();
                inventory = await _inventorySource.InventoryAsync(scope, cancellationToken);
                inventoryMilliseconds = inventoryStopwatch.ElapsedMilliseconds;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception error) when (IsDiscoveryBoundaryError(error))
            {
                if (!IsCurrent(revision, cancellationToken, requestId, scopeRevision)) return null;
                LogFailure(id, scope.GenerationId, "InventoryError", error,
                    "Discovery inventory failed for installation {InstallationId} generation {GenerationId}: {Reason}");
                inventory = new ExecutableInventory(scope, InventoryCompleteness.Incomplete, [],
                    [new InventoryIssue(scope.RootPath, InventoryIssueKind.IoFailure)]);
            }
            if (!IsCurrent(revision, cancellationToken, requestId, scopeRevision)) return null;
            var incoming = new DiscoveryInventoryContext(inventory, installation.Ambiguous);
            ProcessSignatureLearningState state;
            var learningStopwatch = Stopwatch.StartNew();
            if (prepareEpisode)
            {
                var current = _coordinator.GetState(id);
                if (current is null)
                    state = await _coordinator.InitializeAsync(incoming, cancellationToken);
                else
                {
                    if (!await _coordinator.PrepareEpisodeAsync(incoming,
                        current.ConcurrencyToken, cancellationToken)) return null;
                    state = _coordinator.GetState(id)!;
                }
            }
            else state = await _coordinator.InitializeAsync(incoming, cancellationToken);
            Trace.WriteLine($"[DISCOVERY-TIMING] installation={id} totalMs={total.ElapsedMilliseconds} " +
                $"persistenceMs={persistenceMilliseconds} inventoryMs={inventoryMilliseconds} " +
                $"learningMs={learningStopwatch.ElapsedMilliseconds} candidates={inventory.Candidates.Count} " +
                $"issues={inventory.Issues.Count} prepareEpisode={prepareEpisode}");
            lock (_gate)
            {
                if (!IsCurrentLocked(revision, cancellationToken, requestId, scopeRevision))
                    return null;
                _removed.Remove(id);
                if (state.Inventory.Completeness == InventoryCompleteness.Complete)
                    _loggedFailures.Remove(id);
            }
            if (state.Inventory.Completeness != InventoryCompleteness.Complete)
                return null;
            return new DiscoveryInventoryContext(state.Inventory, state.HasAmbiguousInstallation);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception error) when (IsDiscoveryBoundaryError(error))
        {
            if (!IsCurrent(revision, cancellationToken, requestId, scopeRevision))
                return null;
            LogFailure(id, knownGeneration, "PersistenceError", error,
                "Discovery inventory remains pending for installation {InstallationId} generation {GenerationId}: {Reason}");
            return null;
        }
    }

    private static bool IsDiscoveryBoundaryError(Exception error) =>
        error is IOException or UnauthorizedAccessException or Win32Exception or DbException;

    private void LogFailure(InstallationId id, Guid generation, string reason,
        Exception error, string message)
    {
        lock (_gate)
        {
            if (_loggedFailures.TryGetValue(id, out var prior) &&
                prior.Generation == generation && prior.Reason == reason) return;
            _loggedFailures[id] = (generation, reason);
        }
        _logger.LogWarning(error, message, id, generation, reason);
    }

    private bool IsCurrent(long revision, CancellationToken cancellationToken,
        InstallationId? requestId = null, long scopeRevision = 0)
    {
        lock (_gate) return IsCurrentLocked(revision, cancellationToken, requestId, scopeRevision);
    }

    private bool IsCurrentLocked(long revision, CancellationToken cancellationToken,
        InstallationId? requestId = null, long scopeRevision = 0) =>
        !_stopped && !cancellationToken.IsCancellationRequested && _revision == revision &&
        (requestId is null || _scopeRevisions.GetValueOrDefault(requestId.Value) == scopeRevision &&
            _scopeWork.ContainsKey(requestId.Value));

    private sealed record PreparedInstallation(GameInstallation Installation,
        string? Root, bool Ambiguous);

    private sealed class ScopeRequest(PreparedInstallation installation,
        CancellationTokenSource cancellation, long revision, bool queued)
    {
        public PreparedInstallation Installation { get; } = installation;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public long Revision { get; } = revision;
        public bool Queued { get; set; } = queued;
    }
}
