using System.Data.Common;
using System.IO;
using Microsoft.Extensions.Logging;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.UI.Sessions;

/// <summary>Adapts the shared session capture to sequential discovery observations.</summary>
public sealed class ProcessDiscoveryCaptureObserver : IProcessCaptureObserver
{
    private readonly DiscoveryInventoryManager _inventory;
    private readonly ProcessSignatureLearningCoordinator _coordinator;
    private readonly ProcessSignatureAcceptanceService _acceptance;
    private readonly ILogger<ProcessDiscoveryCaptureObserver> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<InstallationId, (Guid Generation, DiscoveryReason Reason)> _logged = [];
    private readonly Dictionary<InstallationId, Guid> _loggedPersistence = [];
    private readonly Dictionary<InstallationId, (DiscoveryReason Reason, string Trigger, bool Name)> _refreshTriggers = [];
    private long _sequence;
    private int _captureGap;

    public ProcessDiscoveryCaptureObserver(DiscoveryInventoryManager inventory,
        ProcessSignatureLearningCoordinator coordinator,
        ProcessSignatureAcceptanceService acceptance,
        ILogger<ProcessDiscoveryCaptureObserver> logger, TimeProvider timeProvider)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _acceptance = acceptance ?? throw new ArgumentNullException(nameof(acceptance));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(timeProvider);
    }

    public void MarkCaptureGap()
    {
        Interlocked.Increment(ref _sequence);
        Interlocked.Exchange(ref _captureGap, 1);
    }

    public async Task ObserveAsync(ProcessCaptureResult capture, DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        await _gate.WaitAsync(cancellationToken);
        InstallationId? processingId = null;
        Guid processingGeneration = Guid.Empty;
        DiscoveryInventoryContext? processingContext = null;
        var preparation = new Dictionary<InstallationId, DiscoveryInventoryContext>();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sequence = Interlocked.Increment(ref _sequence);
            var quality = capture.IsComplete ? EpisodeQuality.Complete : EpisodeQuality.Partial;
            if (Interlocked.Exchange(ref _captureGap, 0) != 0)
                quality |= EpisodeQuality.CaptureGap;
            var batch = new ProcessObservationBatch(sequence, observedAtUtc, quality,
                capture.Processes);
            foreach (var context in _inventory.GetCurrentContexts())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scope = context.Inventory.Scope;
                processingId = scope.InstallationId;
                processingGeneration = scope.GenerationId;
                processingContext = context;
                if (!scope.IsPresent || context.HasAmbiguousInstallation ||
                    context.Inventory.Completeness != InventoryCompleteness.Complete)
                    continue;
                if (_inventory.GetCurrent(scope.InstallationId) is null ||
                    _coordinator.GetState(scope.InstallationId) is null)
                    continue;
                _loggedPersistence.Remove(scope.InstallationId);
                if (_refreshTriggers.TryGetValue(scope.InstallationId, out var priorRefresh) &&
                    (priorRefresh.Reason == DiscoveryReason.IncompleteInventory
                        ? UnknownUnderRootPaths(context, capture).Length == 0
                        : !capture.Processes.Any(process => string.Equals(
                            priorRefresh.Name ? process.ExecutableName : process.ExecutablePath,
                            priorRefresh.Trigger, StringComparison.OrdinalIgnoreCase))))
                    _refreshTriggers.Remove(scope.InstallationId);
                var decision = await _coordinator.ObserveAsync(scope.InstallationId, batch,
                    cancellationToken);
                if (decision is null) continue;
                if (!IsCurrentContext(context)) continue;
                if (decision.Kind != DiscoveryDecisionKind.PromoteMain)
                    LogTransition(scope.InstallationId, scope.GenerationId, decision);
                if (decision.Reasons.Contains(DiscoveryReason.CaptureGap))
                    preparation[scope.InstallationId] = context;
                if (decision.Kind == DiscoveryDecisionKind.PromoteMain)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await _acceptance.TryAcceptAsync(scope.InstallationId, cancellationToken) &&
                        IsCurrentContext(context))
                        _logger.LogInformation(
                            "Discovery signature accepted for installation {InstallationId} generation {GenerationId}",
                            scope.InstallationId, scope.GenerationId);
                    preparation[scope.InstallationId] = context;
                }
                else if (decision.Reasons.Contains(DiscoveryReason.AwaitingIndependentEpisode) ||
                    decision.Reasons.Contains(DiscoveryReason.IncompleteInventory) ||
                    decision.Reasons.Contains(DiscoveryReason.RevisionChanged) ||
                    decision.Reasons.Contains(DiscoveryReason.UnreliablePath))
                {
                    var reason = decision.Reasons[0];
                    if (reason == DiscoveryReason.AwaitingIndependentEpisode)
                        preparation[scope.InstallationId] = context;
                    else
                    {
                        var unknownPaths = UnknownUnderRootPaths(context, capture);
                        var triggerProcess = capture.Processes.FirstOrDefault(process =>
                            unknownPaths.Contains(process.ExecutablePath,
                                StringComparer.OrdinalIgnoreCase));
                        triggerProcess ??= capture.Processes.FirstOrDefault(process =>
                            reason != DiscoveryReason.IncompleteInventory &&
                            process.ExecutablePath is not null &&
                            process.ExecutablePath.StartsWith(
                                scope.RootPath.TrimEnd('\\', '/') + "\\",
                                StringComparison.OrdinalIgnoreCase));
                        triggerProcess ??= capture.Processes.FirstOrDefault(process =>
                            reason == DiscoveryReason.UnreliablePath &&
                            string.IsNullOrWhiteSpace(process.ExecutablePath) &&
                            context.Inventory.Candidates.Any(candidate => string.Equals(
                                candidate.ExecutableName, process.ExecutableName,
                                StringComparison.OrdinalIgnoreCase)));
                        var byName = string.IsNullOrWhiteSpace(triggerProcess?.ExecutablePath);
                        var trigger = byName ? triggerProcess?.ExecutableName : triggerProcess?.ExecutablePath;
                        if (reason == DiscoveryReason.IncompleteInventory && unknownPaths.Length > 0)
                        {
                            byName = false;
                            trigger = string.Join('|', unknownPaths);
                        }
                        trigger ??= reason.ToString();
                        if (!_refreshTriggers.TryGetValue(scope.InstallationId, out var previous) ||
                            previous.Reason != reason || previous.Name != byName ||
                            !string.Equals(previous.Trigger, trigger, StringComparison.OrdinalIgnoreCase))
                        {
                            _refreshTriggers[scope.InstallationId] = (reason, trigger, byName);
                            preparation[scope.InstallationId] = context;
                        }
                    }
                }
            }
            // RequestEpisodePreparation only enqueues inventory work. It runs after every
            // scope has consumed this exact shared batch, outside the observation loop.
            foreach (var (id, context) in preparation)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsCurrentContext(context))
                    _inventory.RequestEpisodePreparation(id, cancellationToken);
            }
        }
        catch (Exception error) when (processingId is not null && processingContext is not null &&
            ProcessSignatureLearningCoordinator.IsLearningAuthorityConflict(error))
        {
            var id = processingId.Value;
            preparation.Remove(id);
            // The conditional Data write refused this episode. Fresh inventory
            // and durable state are prepared off the capture tick; explicit
            // signatures still consume the already captured process snapshot.
            Interlocked.Exchange(ref _captureGap, 1);
            if (IsCurrentContext(processingContext))
                _inventory.RequestEpisodePreparation(id, cancellationToken);
            _logger.LogInformation(error,
                "Discovery learning authority advanced for installation {InstallationId} generation {GenerationId}",
                id, processingGeneration);
            foreach (var (preparedId, context) in preparation)
                if (IsCurrentContext(context))
                    _inventory.RequestEpisodePreparation(preparedId, cancellationToken);
        }
        catch (Exception error) when (processingId is not null && processingContext is not null &&
            error is (IOException or UnauthorizedAccessException or DbException))
        {
            var id = processingId.Value;
            var suspended = _inventory.MarkPending(id, processingContext);
            preparation.Remove(id);
            // Other scopes skipped this shared batch; their next observation is nonqualifying.
            Interlocked.Exchange(ref _captureGap, 1);
            if (!suspended)
                _logger.LogInformation(error,
                    "Stale discovery persistence failure ignored for installation {InstallationId} generation {GenerationId}",
                    id, processingGeneration);
            else if (!_loggedPersistence.TryGetValue(id, out var prior) ||
                     prior != processingGeneration)
            {
                _loggedPersistence[id] = processingGeneration;
                _logger.LogWarning(error,
                    "Discovery persistence unavailable for installation {InstallationId} generation {GenerationId}",
                    id, processingGeneration);
            }
            foreach (var (preparedId, context) in preparation)
                if (IsCurrentContext(context))
                    _inventory.RequestEpisodePreparation(preparedId, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private static string[] UnknownUnderRootPaths(DiscoveryInventoryContext context,
        ProcessCaptureResult capture)
    {
        var scope = context.Inventory.Scope;
        var prefix = scope.RootPath.TrimEnd('\\', '/') + "\\";
        return capture.Processes.Select(process => process.ExecutablePath)
            .Where(path => path is not null &&
                path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                !context.Inventory.Candidates.Any(candidate => string.Equals(
                    candidate.ExecutablePath, path, StringComparison.OrdinalIgnoreCase)))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool IsCurrentContext(DiscoveryInventoryContext context)
    {
        var scope = context.Inventory.Scope;
        var current = _inventory.GetCurrent(scope.InstallationId);
        return ReferenceEquals(current, context) &&
            current?.Inventory.Scope.GenerationId == scope.GenerationId;
    }

    private void LogTransition(InstallationId id, Guid generation, DiscoveryDecision decision)
    {
        var reason = decision.Reasons[0];
        if (_logged.TryGetValue(id, out var prior) &&
            prior.Generation == generation && prior.Reason == reason) return;
        _logged[id] = (generation, reason);
        _logger.LogInformation(
            "Discovery transition for installation {InstallationId} generation {GenerationId}: {DecisionKind} {Reason}",
            id, generation, decision.Kind, reason);
    }
}
