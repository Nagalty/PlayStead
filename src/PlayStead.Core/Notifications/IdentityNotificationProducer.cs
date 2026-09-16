using PlayStead.Core.Identity;
using PlayStead.Core.Library;

namespace PlayStead.Core.Notifications;

public sealed class IdentityNotificationProducer : IIdentityNotificationProducer
{
    private readonly INotificationCenterService _notificationCenter;

    public IdentityNotificationProducer(INotificationCenterService notificationCenter)
    {
        ArgumentNullException.ThrowIfNull(notificationCenter);
        _notificationCenter = notificationCenter;
    }

    public async Task PublishForResolutionAsync(
        GameId gameId,
        IdentityResolutionResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();

        switch (result.State)
        {
            case IdentityResolutionState.MatchProbable:
                await PublishAsync(
                    gameId,
                    "match-probable",
                    "Identité du jeu à vérifier",
                    "Une correspondance probable nécessite votre vérification.",
                    cancellationToken);
                break;

            case IdentityResolutionState.Ambiguous:
                await PublishAsync(
                    gameId,
                    "ambiguous",
                    "Identité du jeu ambiguë",
                    "Plusieurs correspondances sont possibles et nécessitent votre vérification.",
                    cancellationToken);
                break;

            case IdentityResolutionState.MatchConfirmed:
                await ResolveStaleAsync(gameId, cancellationToken);
                break;

            case IdentityResolutionState.New:
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(result),
                    result.State,
                    "Unsupported identity resolution state.");
        }
    }

    private Task<NotificationRecord> PublishAsync(
        GameId gameId,
        string reason,
        string title,
        string message,
        CancellationToken cancellationToken) =>
        _notificationCenter.PublishOrRefreshAsync(
            new NotificationPublishRequest(
                NotificationProducer.IdentityResolution,
                gameId.ToString(),
                reason,
                new NotificationDeduplicationKey(
                    $"identity:{gameId}:{reason}"),
                NotificationPriority.ActionRequired,
                title,
                message,
                PayloadJson: null),
            cancellationToken);

    private async Task ResolveStaleAsync(
        GameId gameId,
        CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            $"identity:{gameId}:match-probable",
            $"identity:{gameId}:ambiguous"
        };

        var active = await _notificationCenter.ListAsync(
            NotificationListFilter.Active,
            cancellationToken);

        foreach (var notification in active)
        {
            if (!keys.Contains(notification.DeduplicationKey))
                continue;

            try
            {
                await _notificationCenter.ResolveAsync(
                    notification.NotificationId,
                    cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                // The active snapshot may race with another resolver.
            }
        }
    }
}
