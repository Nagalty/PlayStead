using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;
using PlayStead.UI.Notifications;
using System.IO;

namespace PlayStead.UI.Tests.Notifications;

public sealed class IdentityDecisionRevocationTests
{
    [Fact]
    public void Xaml_exposes_revoke_choice()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PlayStead.UI", "Notifications", "NotificationPanel.xaml"));
        var xaml = File.ReadAllText(path);
        Assert.Contains("Annuler le choix", xaml);
        Assert.Contains("RevokeConfirmedCommand", xaml);
        Assert.Contains("IsRevokeConfirmedVisible", xaml);
    }

    [Fact]
    public async Task Resolved_identity_with_matching_active_confirmation_exposes_and_revokes_choice()
    {
        var game = GameId.New(); var decision = Confirmed(game); var app = new FakeApplication { Active = decision };
        var notifications = new FakeNotifications(IdentityRecord(game, NotificationState.Resolved));
        var sut = new NotificationCenterViewModel(notifications, app);
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<object?>)sut.SelectFilterCommand).ExecuteAsync("Resolved"); await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
        Assert.True(sut.IsRevokeConfirmedVisible);
        var refreshesBeforeRevoke = notifications.RefreshCount;
        await sut.RevokeConfirmedAsync(CancellationToken.None);
        Assert.Equal(game, app.RevokedGame); Assert.Equal(refreshesBeforeRevoke + 1, notifications.RefreshCount);
        Assert.False(sut.IsRevokeConfirmedVisible);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Revoke_choice_is_hidden_without_matching_active_confirmation(bool activeNotification, bool wrongGame)
    {
        var game = GameId.New(); var decisionGame = wrongGame ? GameId.New() : game;
        var app = new FakeApplication { Active = Confirmed(decisionGame) };
        var sut = new NotificationCenterViewModel(new FakeNotifications(IdentityRecord(game, activeNotification ? NotificationState.Unread : NotificationState.Resolved)), app);
        if (!activeNotification) await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<object?>)sut.SelectFilterCommand).ExecuteAsync("Resolved");
        await sut.RefreshAsync(CancellationToken.None); await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
        Assert.False(sut.IsRevokeConfirmedVisible);
    }

    [Fact]
    public async Task Changing_filter_invalidates_historical_revocation_and_reloads_it_when_resolved_is_selected_again()
    {
        var game = GameId.New(); var app = new FakeApplication { Active = Confirmed(game) };
        var resolved = IdentityRecord(game, NotificationState.Resolved);
        var sut = new NotificationCenterViewModel(new FakeNotifications(resolved), app);
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<object?>)sut.SelectFilterCommand).ExecuteAsync("Resolved"); await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
        Assert.True(sut.IsRevokeConfirmedVisible);
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<object?>)sut.SelectFilterCommand).ExecuteAsync("Active");
        Assert.False(sut.IsRevokeConfirmedVisible);
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<object?>)sut.SelectFilterCommand).ExecuteAsync("Resolved"); await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
        Assert.True(sut.IsRevokeConfirmedVisible);
    }

    [Fact]
    public async Task Revoke_choice_is_hidden_for_null_revoked_rejected_and_non_identity_records()
    {
        var game = GameId.New();
        foreach (var active in new GameIdentityDecision?[] { null, Confirmed(game) with { RevokedUtc = DateTimeOffset.UtcNow }, Confirmed(game) with { DecisionType = IdentityDecisionType.UserRejected } })
        {
            var sut = new NotificationCenterViewModel(new FakeNotifications(IdentityRecord(game, NotificationState.Resolved)), new FakeApplication { Active = active });
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<object?>)sut.SelectFilterCommand).ExecuteAsync("Resolved"); await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
            Assert.False(sut.IsRevokeConfirmedVisible);
        }
        var nonIdentity = new NotificationCenterViewModel(new FakeNotifications(IdentityRecord(game, NotificationState.Resolved) with { Producer = (NotificationProducer)99 }), new FakeApplication { Active = Confirmed(game) });
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<object?>)nonIdentity.SelectFilterCommand).ExecuteAsync("Resolved"); await nonIdentity.SelectAsync(nonIdentity.Items[0].NotificationId, CancellationToken.None);
        Assert.False(nonIdentity.IsRevokeConfirmedVisible);
    }

    [Fact]
    public async Task Revoke_choice_is_hidden_for_invalid_subject_or_non_action_required_priority()
    {
        var game = GameId.New();
        foreach (var record in new[] { IdentityRecord(game, NotificationState.Resolved) with { SubjectId = "not-a-game" }, IdentityRecord(game, NotificationState.Resolved) with { Priority = NotificationPriority.Info } })
        {
            var sut = new NotificationCenterViewModel(new FakeNotifications(record), new FakeApplication { Active = Confirmed(game) });
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<object?>)sut.SelectFilterCommand).ExecuteAsync("Resolved"); await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
            Assert.False(sut.IsRevokeConfirmedVisible);
        }
    }

    [Fact]
    public async Task Revocation_propagates_cancellation_and_does_not_refresh_after_failure()
    {
        var game = GameId.New(); using var cts = new CancellationTokenSource(); cts.Cancel();
        var app = new FakeApplication { Active = Confirmed(game), ThrowCancellation = true };
        var notifications = new FakeNotifications(IdentityRecord(game, NotificationState.Resolved)); var sut = new NotificationCenterViewModel(notifications, app);
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<object?>)sut.SelectFilterCommand).ExecuteAsync("Resolved"); await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
        var before = notifications.RefreshCount;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.RevokeConfirmedAsync(cts.Token));
        Assert.Equal(before, notifications.RefreshCount); Assert.True(sut.IsRevokeConfirmedVisible);
    }

    [Fact]
    public async Task Revocation_business_failure_does_not_refresh_and_passes_exact_token()
    {
        var game = GameId.New(); using var cts = new CancellationTokenSource();
        var app = new FakeApplication { Active = Confirmed(game), ThrowFailure = true };
        var notifications = new FakeNotifications(IdentityRecord(game, NotificationState.Resolved)); var sut = new NotificationCenterViewModel(notifications, app);
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<object?>)sut.SelectFilterCommand).ExecuteAsync("Resolved"); await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
        var before = notifications.RefreshCount;
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RevokeConfirmedAsync(cts.Token));
        Assert.Equal(cts.Token, app.LastRevokeToken); Assert.Equal(before, notifications.RefreshCount); Assert.True(sut.IsRevokeConfirmedVisible);
    }

    [Fact]
    public async Task Revoke_command_locks_concurrent_actions()
    {
        var game = GameId.New(); var app = new FakeApplication { Active = Confirmed(game), BlockRevocation = true };
        var sut = new NotificationCenterViewModel(new FakeNotifications(IdentityRecord(game, NotificationState.Resolved)), app);
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<object?>)sut.SelectFilterCommand).ExecuteAsync("Resolved"); await sut.SelectAsync(sut.Items[0].NotificationId, CancellationToken.None);
        var running = sut.RevokeConfirmedCommand.ExecuteAsync(null); await app.RevokeStarted.Task;
        Assert.True(sut.IsDecisionActionInProgress); Assert.False(sut.RevokeConfirmedCommand.CanExecute(null)); Assert.False(sut.ConfirmDecisionCommand.CanExecute(null)); Assert.False(sut.RejectSingleCandidateCommand.CanExecute(null)); Assert.False(sut.RejectDecisionCommand.CanExecute(null)); Assert.False(sut.ChooseDecisionCommand.CanExecute(null));
        await sut.RevokeConfirmedAsync(CancellationToken.None); Assert.Equal(1, app.RevokeCalls);
        app.ReleaseRevoke.SetResult(true); await running;
        Assert.True(sut.RevokeConfirmedCommand.CanExecute(null)); Assert.True(sut.ConfirmDecisionCommand.CanExecute(null)); Assert.True(sut.RejectSingleCandidateCommand.CanExecute(null)); Assert.True(sut.RejectDecisionCommand.CanExecute(null)); Assert.True(sut.ChooseDecisionCommand.CanExecute(null));
    }

    private static GameIdentityDecision Confirmed(GameId game) => new(IdentityDecisionId.New(), game, CatalogContentId.New(), IdentityDecisionType.UserConfirmed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
    private static NotificationRecord IdentityRecord(GameId game, NotificationState state) => new(NotificationId.New(), NotificationProducer.IdentityResolution, game.ToString(), "match-probable", "identity:key", NotificationPriority.ActionRequired, state, "t", "m", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, state == NotificationState.Resolved ? DateTimeOffset.UtcNow : null);

    private sealed class FakeApplication : IIdentityDecisionApplicationService
    {
        public GameIdentityDecision? Active { get; set; }
        public GameId? RevokedGame { get; private set; }
        public bool ThrowCancellation { get; set; }
        public bool ThrowFailure { get; set; }
        public bool BlockRevocation { get; set; }
        public int RevokeCalls { get; private set; }
        public CancellationToken LastRevokeToken { get; private set; }
        public TaskCompletionSource<bool> RevokeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseRevoke { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IdentityDecisionContext?> GetContextAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult<IdentityDecisionContext?>(null);
        public Task ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RejectAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<GameIdentityDecision?> GetActiveConfirmedAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult(Active);
        public async Task RevokeConfirmedAsync(GameId gameId, CancellationToken cancellationToken) { RevokeCalls++; LastRevokeToken = cancellationToken; cancellationToken.ThrowIfCancellationRequested(); if (ThrowCancellation) throw new OperationCanceledException(cancellationToken); if (ThrowFailure) throw new InvalidOperationException(); RevokedGame = gameId; if (BlockRevocation) { RevokeStarted.SetResult(true); await ReleaseRevoke.Task.WaitAsync(cancellationToken); } Active = null; }
    }

    private sealed class FakeNotifications(NotificationRecord item) : INotificationCenterService
    {
        public int RefreshCount { get; private set; }
        public Task<int> GetActiveCountAsync(CancellationToken cancellationToken) { RefreshCount++; return Task.FromResult(0); }
        public Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter filter, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<NotificationRecord>>(filter == NotificationListFilter.Resolved && item.State == NotificationState.Resolved || filter == NotificationListFilter.Active && item.State != NotificationState.Resolved ? [item] : []);
        public Task<NotificationRecord> MarkReadAsync(NotificationId id, CancellationToken cancellationToken) => Task.FromResult(item with { State = NotificationState.Read });
        public Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NotificationRecord> ResolveAsync(NotificationId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> PurgeExpiredResolvedAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
