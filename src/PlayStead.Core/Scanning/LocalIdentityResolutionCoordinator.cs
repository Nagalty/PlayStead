using PlayStead.Core.Identity;
using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
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
    private readonly IIdentityDecisionStore? _identityDecisionStore;
    private readonly IIdentityNotificationProducer? _notificationProducer;
    private readonly CanonicalProviderIdentityLinker? _providerIdentityLinker;

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
        : this(libraryGameLookup, identityResolver, identityResolutionStore, identityReconciler, identityDecisionStore: null, notificationProducer)
    {
    }

    public LocalIdentityResolutionCoordinator(
        ILibraryGameLookup libraryGameLookup,
        IGameIdentityResolver identityResolver,
        IIdentityResolutionStore identityResolutionStore,
        ILocalIdentityReconciler identityReconciler,
        IIdentityDecisionStore? identityDecisionStore)
        : this(libraryGameLookup, identityResolver, identityResolutionStore, identityReconciler, identityDecisionStore, notificationProducer: null)
    {
    }

    public LocalIdentityResolutionCoordinator(
        ILibraryGameLookup libraryGameLookup,
        IGameIdentityResolver identityResolver,
        IIdentityResolutionStore identityResolutionStore,
        ILocalIdentityReconciler identityReconciler,
        IIdentityDecisionStore? identityDecisionStore,
        IIdentityNotificationProducer? notificationProducer,
        ICanonicalCatalogStore? catalogStore = null,
        IProviderIdentityStore? providerIdentityStore = null)
    {
        ArgumentNullException.ThrowIfNull(libraryGameLookup);
        ArgumentNullException.ThrowIfNull(identityResolver);
        ArgumentNullException.ThrowIfNull(identityResolutionStore);
        ArgumentNullException.ThrowIfNull(identityReconciler);

        _libraryGameLookup = libraryGameLookup;
        _identityResolver = identityResolver;
        _identityResolutionStore = identityResolutionStore;
        _identityReconciler = identityReconciler;
        _identityDecisionStore = identityDecisionStore;
        _notificationProducer = notificationProducer;
        _providerIdentityLinker = catalogStore is not null && providerIdentityStore is not null
            ? new CanonicalProviderIdentityLinker(catalogStore, providerIdentityStore)
            : null;
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

            if (_identityDecisionStore is not null)
            {
                var confirmed = await _identityDecisionStore
                    .GetActiveConfirmedAsync(gameId.Value, cancellationToken);
                if (confirmed is not null)
                    continue;
            }

            var resolution = await _identityResolver.ResolveAsync(
                new GameIdentityObservation(
                    catalogProvider,
                    installation.ExternalId,
                    installation.Title,
                    installation.ObservedAtUtc),
                cancellationToken);

            if (_identityDecisionStore is not null &&
                resolution.CandidateContentId is { } resolvedCandidate)
            {
                var rejected = await _identityDecisionStore
                    .ListActiveRejectedAsync(gameId.Value, cancellationToken);
                if (rejected.Any(decision =>
                        decision.CatalogContentId == resolvedCandidate))
                {
                    resolution = resolution with
                    {
                        State = IdentityResolutionState.New,
                        CandidateContentId = null
                    };
                }
            }

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
                    if (_providerIdentityLinker is not null)
                    {
                        await _providerIdentityLinker.SyncAsync(
                            gameId.Value,
                            candidate,
                            cancellationToken);
                    }
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
