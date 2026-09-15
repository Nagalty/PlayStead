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
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sequence = Interlocked.Increment(ref _sequence);
            var quality = capture.IsComplete ? EpisodeQuality.Complete : EpisodeQuality.Partial;
            if (Interlocked.Exchange(ref _captureGap, 0) != 0)
                quality |= EpisodeQuality.CaptureGap;
            var batch = new ProcessObservationBatch(sequence, observedAtUtc, quality,
                capture.Processes);
            var preparation = new HashSet<InstallationId>();
            foreach (var context in _inventory.GetCurrentContexts())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var scope = context.Inventory.Scope;
                if (!scope.IsPresent || context.HasAmbiguousInstallation ||
                    context.Inventory.Completeness != InventoryCompleteness.Complete)
                    continue;
                if (_inventory.GetCurrent(scope.InstallationId) is null ||
                    _coordinator.GetState(scope.InstallationId) is null)
                    continue;
                if (_refreshTriggers.TryGetValue(scope.InstallationId, out var priorRefresh) &&
                    !capture.Processes.Any(process => string.Equals(
                        priorRefresh.Name ? process.ExecutableName : process.ExecutablePath,
                        priorRefresh.Trigger, StringComparison.OrdinalIgnoreCase)))
                    _refreshTriggers.Remove(scope.InstallationId);
                var decision = await _coordinator.ObserveAsync(scope.InstallationId, batch,
                    cancellationToken);
                if (decision is null) continue;
                if (decision.Kind != DiscoveryDecisionKind.PromoteMain)
                    LogTransition(scope.InstallationId, scope.GenerationId, decision);
                if (decision.Kind == DiscoveryDecisionKind.PromoteMain)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await _acceptance.TryAcceptAsync(scope.InstallationId, cancellationToken))
                        _logger.LogInformation(
                            "Discovery signature accepted for installation {InstallationId} generation {GenerationId}",
                            scope.InstallationId, scope.GenerationId);
                    preparation.Add(scope.InstallationId);
                }
                else if (decision.Reasons.Contains(DiscoveryReason.AwaitingIndependentEpisode) ||
                    decision.Reasons.Contains(DiscoveryReason.IncompleteInventory) ||
                    decision.Reasons.Contains(DiscoveryReason.RevisionChanged) ||
                    decision.Reasons.Contains(DiscoveryReason.UnreliablePath))
                {
                    var reason = decision.Reasons[0];
                    if (reason == DiscoveryReason.AwaitingIndependentEpisode)
                        preparation.Add(scope.InstallationId);
                    else
                    {
                        var triggerProcess = capture.Processes.FirstOrDefault(process =>
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
                        trigger ??= reason.ToString();
                        if (!_refreshTriggers.TryGetValue(scope.InstallationId, out var previous) ||
                            previous.Reason != reason || previous.Name != byName ||
                            !string.Equals(previous.Trigger, trigger, StringComparison.OrdinalIgnoreCase))
                        {
                            _refreshTriggers[scope.InstallationId] = (reason, trigger, byName);
                            preparation.Add(scope.InstallationId);
                        }
                    }
                }
            }
            // RequestEpisodePreparation only enqueues inventory work. It runs after every
            // scope has consumed this exact shared batch, outside the observation loop.
            foreach (var id in preparation)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _inventory.RequestEpisodePreparation(id, cancellationToken);
            }
        }
        finally { _gate.Release(); }
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
