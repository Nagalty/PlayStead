using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;
using PlayStead.UI.Notifications;
using System.IO;

namespace PlayStead.UI.Tests.Notifications;

public sealed class IdentityDecisionNotificationUiTests
{
    [Fact]
    public void Xaml_uses_ancestor_bindings_and_distinct_visibility_sections()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PlayStead.UI", "Notifications", "NotificationPanel.xaml"));
        var xaml = File.ReadAllText(path);
        Assert.Contains("ChooseDecisionCommand, RelativeSource={RelativeSource AncestorType=UserControl}", xaml);
        Assert.Contains("RejectDecisionCommand, RelativeSource={RelativeSource AncestorType=UserControl}", xaml);
        Assert.Contains("IsAmbiguousDecisionVisible", xaml);
        Assert.Contains("IsSingleCandidateDecisionVisible", xaml);
    }

    [Fact]
    public async Task Null_context_exposes_no_decision_action()
    {
        var app = new RecordingApplication { Context = null };
        var sut = new NotificationCenterViewModel(new RecordingNotifications(IdentityRecord()), app);

        await sut.RefreshAsync(CancellationToken.None);
        await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);

        Assert.False(sut.IsDecisionActionVisible);
        Assert.Empty(sut.DecisionCandidates);
    }

    [Fact]
    public async Task Match_probable_confirm_passes_exact_ids_and_refreshes()
    {
        var game = GameId.New();
        var candidate = CatalogContentId.New();
        var app = new RecordingApplication { Context = IdentityDecisionContext.Create(game, IdentityResolutionState.MatchProbable, [new(candidate)]) };
        var notifications = new RecordingNotifications(IdentityRecord(game));
        var sut = new NotificationCenterViewModel(notifications, app);

        await sut.RefreshAsync(CancellationToken.None);
        await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
        var refreshesBeforeAction = notifications.RefreshCount;
        await sut.ConfirmAsync(CancellationToken.None);

        Assert.Equal((game, candidate), app.Confirmed);
        Assert.Equal(refreshesBeforeAction + 1, notifications.RefreshCount);
        Assert.True(sut.IsSingleCandidateDecisionVisible);
        Assert.False(sut.IsAmbiguousDecisionVisible);
    }

    [Fact]
    public async Task Ambiguous_reject_passes_selected_candidate_and_exposes_candidates()
    {
        var game = GameId.New();
        var first = CatalogContentId.New();
        var second = CatalogContentId.New();
        var app = new RecordingApplication { Context = IdentityDecisionContext.Create(game, IdentityResolutionState.Ambiguous, [new(first), new(second)]) };
        var sut = new NotificationCenterViewModel(new RecordingNotifications(IdentityRecord(game, "ambiguous")), app);

        await sut.RefreshAsync(CancellationToken.None);
        await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
        await sut.RejectAsync(first, CancellationToken.None);

        Assert.Equal((game, first), app.Rejected);
        Assert.Equal([first, second], sut.DecisionCandidates);
        Assert.False(sut.IsSingleCandidateDecisionVisible);
        Assert.True(sut.IsAmbiguousDecisionVisible);
    }

    [Fact]
    public async Task Non_identity_notification_exposes_no_decision_action()
    {
        var app = new RecordingApplication { Context = IdentityDecisionContext.Create(GameId.New(), IdentityResolutionState.MatchProbable, [new(CatalogContentId.New())]) };
        var sut = new NotificationCenterViewModel(new RecordingNotifications(new(NotificationId.New(), (NotificationProducer)99, "x", "other", "other", NotificationPriority.ActionRequired, NotificationState.Unread, "t", "m", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null)), app);

        await sut.RefreshAsync(CancellationToken.None);
        await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);

        Assert.False(sut.IsDecisionActionVisible);
        Assert.Null(app.RequestedGame);
    }

    [Fact]
    public async Task Cancellation_is_propagated_when_loading_context()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var game = GameId.New();
        var app = new RecordingApplication { Context = IdentityDecisionContext.Create(game, IdentityResolutionState.MatchProbable, [new(CatalogContentId.New())]) };
        var sut = new NotificationCenterViewModel(new RecordingNotifications(IdentityRecord(game)), app);
        await sut.RefreshAsync(CancellationToken.None);
        await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
        app.ThrowCancellation = true;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.LoadDecisionContextAsync(cts.Token));
        Assert.Equal(cts.Token, app.LastToken);
    }

    [Fact]
    public async Task Commands_pass_exact_ids_and_lock_concurrent_actions()
    {
        var game = GameId.New(); var first = CatalogContentId.New(); var second = CatalogContentId.New();
        var app = new RecordingApplication { Context = IdentityDecisionContext.Create(game, IdentityResolutionState.Ambiguous, [new(first), new(second)]), BlockActions = true };
        var sut = new NotificationCenterViewModel(new RecordingNotifications(IdentityRecord(game, "ambiguous")), app);
        await sut.RefreshAsync(CancellationToken.None); await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
        var running = sut.ChooseDecisionCommand.ExecuteAsync(first);
        await app.ActionStarted.Task;
        Assert.True(sut.IsDecisionActionInProgress);
        Assert.False(sut.ConfirmDecisionCommand.CanExecute(null));
        Assert.False(sut.RejectSingleCandidateCommand.CanExecute(null));
        Assert.False(sut.ChooseDecisionCommand.CanExecute(first));
        Assert.False(sut.RejectDecisionCommand.CanExecute(second));
        await sut.RejectDecisionCommand.ExecuteAsync(second);
        await sut.ChooseDecisionCommand.ExecuteAsync(second);
        Assert.Equal(1, app.ConfirmCallCount);
        Assert.Equal(0, app.RejectCallCount);
        app.ReleaseActions.SetResult(true); await running;
        Assert.Equal((game, first), app.Confirmed);
    }

    private static NotificationRecord IdentityRecord(GameId? game = null, string reason = "match-probable") => new(NotificationId.New(), NotificationProducer.IdentityResolution, (game ?? GameId.New()).ToString(), reason, "identity:key", NotificationPriority.ActionRequired, NotificationState.Unread, "t", "m", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);

    private sealed class RecordingApplication : IIdentityDecisionApplicationService
    {
        public IdentityDecisionContext? Context { get; set; }
        public (GameId, CatalogContentId)? Confirmed { get; private set; }
        public (GameId, CatalogContentId)? Rejected { get; private set; }
        public GameId? RequestedGame { get; private set; }
        public bool ThrowCancellation { get; set; }
        public CancellationToken LastToken { get; private set; }
        public bool BlockActions { get; set; }
        public int ConfirmCallCount { get; private set; }
        public int RejectCallCount { get; private set; }
        public TaskCompletionSource<bool> ActionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseActions { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IdentityDecisionContext?> GetContextAsync(GameId gameId, CancellationToken cancellationToken) { LastToken = cancellationToken; cancellationToken.ThrowIfCancellationRequested(); RequestedGame = gameId; if (ThrowCancellation) throw new OperationCanceledException(cancellationToken); return Task.FromResult(Context); }
        public async Task ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken) { ConfirmCallCount++; Confirmed = (gameId, catalogContentId); if (BlockActions) { ActionStarted.SetResult(true); await ReleaseActions.Task.WaitAsync(cancellationToken); } }
        public Task RejectAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken) { RejectCallCount++; Rejected = (gameId, catalogContentId); return Task.CompletedTask; }
    }

    private sealed class RecordingNotifications(NotificationRecord item) : INotificationCenterService
    {
        public int RefreshCount { get; private set; }
        public Task<int> GetActiveCountAsync(CancellationToken cancellationToken) { RefreshCount++; return Task.FromResult(1); }
        public Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter filter, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NotificationRecord>>([item]);
        public Task<NotificationRecord> MarkReadAsync(NotificationId id, CancellationToken cancellationToken) => Task.FromResult(item with { State = NotificationState.Read });
        public Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NotificationRecord> ResolveAsync(NotificationId id, CancellationToken cancellationToken) => Task.FromResult(item);
        public Task<int> PurgeExpiredResolvedAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
