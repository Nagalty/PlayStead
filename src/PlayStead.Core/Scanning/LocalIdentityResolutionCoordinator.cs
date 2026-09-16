using PlayStead.Core.Identity;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Scanning;

public sealed class LocalIdentityResolutionCoordinator
    : ILocalIdentityResolutionCoordinator
{
    private readonly ILibraryGameLookup _libraryGameLookup;
    private readonly IGameIdentityResolver _identityResolver;
    private readonly IIdentityResolutionStore _identityResolutionStore;
    private readonly ILocalIdentityReconciler _identityReconciler;
    private readonly IIdentityNotificationProducer? _notificationProducer;

    public LocalIdentityResolutionCoordinator(
        ILibraryGameLookup libraryGameLookup,
        IGameIdentityResolver identityResolver,
        IIdentityResolutionStore identityResolutionStore,
        ILocalIdentityReconciler identityReconciler)
        : this(
            libraryGameLookup,
            identityResolver,
            identityResolutionStore,
            identityReconciler,
            notificationProducer: null)
    {
    }

    public LocalIdentityResolutionCoordinator(
        ILibraryGameLookup libraryGameLookup,
        IGameIdentityResolver identityResolver,
        IIdentityResolutionStore identityResolutionStore,
        ILocalIdentityReconciler identityReconciler,
        IIdentityNotificationProducer? notificationProducer)
    {
        ArgumentNullException.ThrowIfNull(libraryGameLookup);
        ArgumentNullException.ThrowIfNull(identityResolver);
        ArgumentNullException.ThrowIfNull(identityResolutionStore);
        ArgumentNullException.ThrowIfNull(identityReconciler);

        _libraryGameLookup = libraryGameLookup;
        _identityResolver = identityResolver;
        _identityResolutionStore = identityResolutionStore;
        _identityReconciler = identityReconciler;
        _notificationProducer = notificationProducer;
    }

    public async Task ResolveAfterScanAsync(
        SourceScanResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        foreach (var installation in result.Installations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ProviderKindMapping.TryMap(
                    installation.Provider,
                    out var catalogProvider))
            {
                continue;
            }

            var gameId =
                await _libraryGameLookup.FindGameIdByProviderRefAsync(
                    installation.Provider,
                    installation.ExternalId,
                    cancellationToken);

            if (gameId is null)
                continue;

            var resolution = await _identityResolver.ResolveAsync(
                new GameIdentityObservation(
                    catalogProvider,
                    installation.ExternalId,
                    installation.Title,
                    installation.ObservedAtUtc),
                cancellationToken);

            switch (resolution.State)
            {
                case IdentityResolutionState.MatchConfirmed:
                    var candidate = resolution.CandidateContentId
                        ?? throw new InvalidOperationException(
                            "A confirmed identity requires a candidate content ID.");
                    await _identityReconciler.ReconcileAsync(
                        gameId.Value,
                        candidate,
                        resolution.Evidence,
                        installation.ObservedAtUtc,
                        cancellationToken);
                    break;

                case IdentityResolutionState.New:
                    await _identityResolutionStore
                        .GetOrCreateProvisionalAsync(
                            gameId.Value,
                            resolution.Evidence,
                            installation.ObservedAtUtc,
                            cancellationToken);
                    break;

                case IdentityResolutionState.MatchProbable:
                case IdentityResolutionState.Ambiguous:
                    var provisional = await _identityResolutionStore
                        .GetOrCreateProvisionalAsync(
                            gameId.Value,
                            resolution.Evidence,
                            installation.ObservedAtUtc,
                            cancellationToken);
                    await _identityResolutionStore.UpsertAsync(
                        provisional with
                        {
                            State = resolution.State,
                            CandidateContentId =
                                resolution.State == IdentityResolutionState.Ambiguous
                                    ? null
                                    : resolution.CandidateContentId,
                            Evidence = resolution.Evidence,
                            UpdatedAtUtc = installation.ObservedAtUtc
                        },
                        cancellationToken);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(resolution),
                        resolution.State,
                        "Unsupported identity resolution state.");
            }

            if (_notificationProducer is not null)
            {
                try
                {
                    await _notificationProducer.PublishForResolutionAsync(
                        gameId.Value,
                        resolution,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Notifications are a non-critical identity side effect.
                }
            }
        }
    }
}
