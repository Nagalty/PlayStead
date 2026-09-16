using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;

namespace PlayStead.Core.Tests.Notifications;

public sealed class IdentityNotificationProducerTests
{
    private static readonly GameId GameId = new(
        Guid.Parse("00000000-0000-4000-8000-000000000001"));

    [Theory]
    [InlineData(IdentityResolutionState.MatchProbable, "match-probable")]
    [InlineData(IdentityResolutionState.Ambiguous, "ambiguous")]
    public async Task Actionable_resolution_publishes_stable_action_required_notification(
        IdentityResolutionState state,
        string reason)
    {
        var center = new RecordingNotificationCenter();
        var sut = new IdentityNotificationProducer(center);

        await sut.PublishForResolutionAsync(
            GameId,
            Result(state),
            CancellationToken.None);

        var request = Assert.Single(center.Published);
        Assert.Equal(NotificationProducer.IdentityResolution, request.Producer);
        Assert.Equal(NotificationPriority.ActionRequired, request.Priority);
        Assert.Equal(GameId.ToString(), request.SubjectId);
        Assert.Equal(reason, request.Reason);
        Assert.Equal($"identity:{GameId}:{reason}", request.DeduplicationKey.Value);
        Assert.False(string.IsNullOrWhiteSpace(request.Title));
        Assert.False(string.IsNullOrWhiteSpace(request.Message));
    }

    [Fact]
    public async Task Repeated_same_state_uses_same_deduplication_key()
    {
        var center = new RecordingNotificationCenter();
        var sut = new IdentityNotificationProducer(center);

        await sut.PublishForResolutionAsync(GameId, Result(IdentityResolutionState.MatchProbable), CancellationToken.None);
        await sut.PublishForResolutionAsync(GameId, Result(IdentityResolutionState.MatchProbable), CancellationToken.None);

        Assert.Equal(2, center.Published.Count);
        Assert.Equal(center.Published[0].DeduplicationKey, center.Published[1].DeduplicationKey);
    }

    [Fact]
    public async Task Match_confirmed_resolves_both_stale_identity_notifications_without_publishing()
    {
        var center = new RecordingNotificationCenter();
        var probable = Notification("match-probable");
        var ambiguous = Notification("ambiguous");
        var unrelated = Notification("other", Game(2));
        center.Active.AddRange([probable, ambiguous, unrelated]);
        var sut = new IdentityNotificationProducer(center);

        await sut.PublishForResolutionAsync(GameId, Result(IdentityResolutionState.MatchConfirmed), CancellationToken.None);

        Assert.Empty(center.Published);
        Assert.Equal([probable.NotificationId, ambiguous.NotificationId], center.Resolved);
    }

    [Fact]
    public async Task Missing_stale_notification_during_confirmed_cleanup_is_ignored()
    {
        var center = new RecordingNotificationCenter { ThrowMissingOnResolve = true };
        center.Active.Add(Notification("ambiguous"));
        var sut = new IdentityNotificationProducer(center);

        await sut.PublishForResolutionAsync(GameId, Result(IdentityResolutionState.MatchConfirmed), CancellationToken.None);

        Assert.Empty(center.Published);
    }

    [Fact]
    public async Task New_does_not_publish_or_resolve()
    {
        var center = new RecordingNotificationCenter();
        var sut = new IdentityNotificationProducer(center);

        await sut.PublishForResolutionAsync(GameId, Result(IdentityResolutionState.New), CancellationToken.None);

        Assert.Empty(center.Published);
        Assert.Empty(center.Resolved);
        Assert.Equal(0, center.ListCalls);
    }

    [Fact]
    public async Task Cancellation_is_propagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sut = new IdentityNotificationProducer(new RecordingNotificationCenter());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.PublishForResolutionAsync(GameId, Result(IdentityResolutionState.Ambiguous), cancellation.Token));
    }

    private static IdentityResolutionResult Result(IdentityResolutionState state) =>
        new(
            state,
            state is IdentityResolutionState.MatchConfirmed or IdentityResolutionState.MatchProbable
                ? CatalogContentId.New()
                : null,
            new IdentityResolutionEvidence(
                IdentityResolutionEvidenceKind.NoExactProviderRefMatch,
                CatalogProviderKind.Steam,
                "1874880",
                null));

    private static NotificationRecord Notification(string reason, GameId? gameId = null)
    {
        var subject = (gameId ?? GameId).ToString();
        return new(
            NotificationId.New(),
            NotificationProducer.IdentityResolution,
            subject,
            reason,
            $"identity:{subject}:{reason}",
            NotificationPriority.ActionRequired,
            NotificationState.Unread,
            "Title",
            "Message",
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null);
    }

    private static GameId Game(int index) => new(
        Guid.Parse($"00000000-0000-4000-8000-{index:000000000000}"));

    private sealed class RecordingNotificationCenter : INotificationCenterService
    {
        public List<NotificationPublishRequest> Published { get; } = [];
        public List<NotificationRecord> Active { get; } = [];
        public List<NotificationId> Resolved { get; } = [];
        public int ListCalls { get; private set; }
        public bool ThrowMissingOnResolve { get; init; }

        public Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Published.Add(request);
            return Task.FromResult(Notification(request.Reason));
        }

        public Task<NotificationRecord> ResolveAsync(NotificationId id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowMissingOnResolve)
                throw new KeyNotFoundException();
            Resolved.Add(id);
            return Task.FromResult(Active.Single(x => x.NotificationId == id) with { State = NotificationState.Resolved });
        }

        public Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter filter, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ListCalls++;
            return Task.FromResult<IReadOnlyList<NotificationRecord>>(Active);
        }

        public Task<NotificationRecord> MarkReadAsync(NotificationId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> GetActiveCountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> PurgeExpiredResolvedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
