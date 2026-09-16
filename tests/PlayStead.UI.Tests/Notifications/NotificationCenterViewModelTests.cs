using CommunityToolkit.Mvvm.Input;
using PlayStead.Core.Notifications;
using PlayStead.UI.Notifications;

namespace PlayStead.UI.Tests.Notifications;

public sealed class NotificationCenterViewModelTests
{
    [Fact]
    public async Task Toggle_panel_command_opens_then_closes_without_marking_read()
    {
        var service = new RecordingService();
        var sut = new NotificationCenterViewModel(service);

        await Assert.IsAssignableFrom<IAsyncRelayCommand>(sut.TogglePanelCommand).ExecuteAsync(null);
        Assert.True(sut.IsPanelOpen);
        await Assert.IsAssignableFrom<IAsyncRelayCommand>(sut.TogglePanelCommand).ExecuteAsync(null);
        Assert.False(sut.IsPanelOpen);
        Assert.Empty(service.MarkedRead);
    }

    [Fact]
    public async Task Badge_visibility_tracks_active_count_and_notifies_when_count_changes()
    {
        var service = new RecordingService { ActiveCount = 0 };
        var sut = new NotificationCenterViewModel(service);
        var names = new List<string?>();
        sut.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        await sut.RefreshAsync(CancellationToken.None);
        Assert.False(sut.IsBadgeVisible);
        service.ActiveCount = 2;
        await sut.RefreshAsync(CancellationToken.None);

        Assert.True(sut.IsBadgeVisible);
        Assert.Contains(nameof(NotificationCenterViewModel.IsBadgeVisible), names);
    }
    [Fact]
    public async Task Refresh_uses_active_count_and_active_items_without_marking_read()
    {
        var service = new RecordingService
        {
            ActiveCount = 2,
            Active = [Record("unread", NotificationState.Unread), Record("read", NotificationState.Read)]
        };
        var sut = new NotificationCenterViewModel(service);

        await sut.RefreshAsync(CancellationToken.None);

        Assert.Equal(2, sut.ActiveCount);
        Assert.Equal(service.Active, sut.Items);
        Assert.Equal(NotificationListFilter.Active, sut.Filter);
        Assert.Empty(service.MarkedRead);
    }

    [Fact]
    public async Task Opening_refreshes_without_marking_records_read_and_second_toggle_closes()
    {
        var service = new RecordingService();
        var sut = new NotificationCenterViewModel(service);

        await Assert.IsAssignableFrom<IAsyncRelayCommand>(sut.TogglePanelCommand).ExecuteAsync(null);
        Assert.True(sut.IsPanelOpen);
        Assert.Empty(service.MarkedRead);

        await Assert.IsAssignableFrom<IAsyncRelayCommand>(sut.TogglePanelCommand).ExecuteAsync(null);
        Assert.False(sut.IsPanelOpen);
        Assert.Empty(service.MarkedRead);
    }

    [Fact]
    public async Task Selecting_unread_marks_only_that_notification_and_keeps_it_active()
    {
        var service = new RecordingService
        {
            ActiveCount = 1,
            Active = [Record("one", NotificationState.Unread)]
        };
        var sut = new NotificationCenterViewModel(service);
        await sut.RefreshAsync(CancellationToken.None);

        await sut.SelectAsync(service.Active[0].NotificationId, CancellationToken.None);

        Assert.Equal([service.Active[0].NotificationId], service.MarkedRead);
        Assert.Equal(1, sut.ActiveCount);
    }

    [Fact]
    public async Task Selecting_read_does_not_resolve_or_mark_again()
    {
        var service = new RecordingService
        {
            Active = [Record("read", NotificationState.Read)]
        };
        var sut = new NotificationCenterViewModel(service);
        await sut.RefreshAsync(CancellationToken.None);

        await sut.SelectAsync(service.Active[0].NotificationId, CancellationToken.None);

        Assert.Empty(service.MarkedRead);
        Assert.Empty(service.ResolvedIds);
    }

    [Fact]
    public async Task Selecting_filter_refreshes_the_requested_view()
    {
        var service = new RecordingService();
        var sut = new NotificationCenterViewModel(service);

        await Assert.IsAssignableFrom<IAsyncRelayCommand>(sut.SelectFilterCommand).ExecuteAsync(NotificationListFilter.Resolved);

        Assert.Equal(NotificationListFilter.Resolved, sut.Filter);
        Assert.Equal(NotificationListFilter.Resolved, service.LastFilter);
    }

    [Fact]
    public async Task Selection_exposes_the_selected_notification_detail()
    {
        var service = new RecordingService { Active = [Record("detail", NotificationState.Unread)] };
        var sut = new NotificationCenterViewModel(service);
        await sut.RefreshAsync(CancellationToken.None);

        await sut.SelectAsync(service.Active[0].NotificationId, CancellationToken.None);

        Assert.Equal(service.Active[0].NotificationId, sut.SelectedItem?.NotificationId);
        Assert.Equal(NotificationState.Read, sut.SelectedItem?.State);
    }

    [Fact]
    public async Task Cancellation_is_propagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = new NotificationCenterViewModel(new RecordingService());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.RefreshAsync(cts.Token));
    }

    private static NotificationRecord Record(string reason, NotificationState state) =>
        new(NotificationId.New(), NotificationProducer.IdentityResolution, "subject", reason, "key-" + reason, NotificationPriority.ActionRequired, state, "Title " + reason, "Message " + reason, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, state == NotificationState.Read ? DateTimeOffset.UtcNow : null, state == NotificationState.Resolved ? DateTimeOffset.UtcNow : null);

    private sealed class RecordingService : INotificationCenterService
    {
        public int ActiveCount { get; set; }
        public IReadOnlyList<NotificationRecord> Active { get; set; } = [];
        public IReadOnlyList<NotificationRecord> ResolvedItems { get; set; } = [];
        public NotificationListFilter LastFilter { get; private set; } = NotificationListFilter.Active;
        public List<NotificationId> MarkedRead { get; } = [];
        public List<NotificationId> ResolvedIds { get; } = [];

        public Task<int> GetActiveCountAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(ActiveCount); }
        public Task<IReadOnlyList<NotificationRecord>> ListAsync(NotificationListFilter filter, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); LastFilter = filter; return Task.FromResult(filter == NotificationListFilter.Active ? Active : ResolvedItems); }
        public Task<NotificationRecord> MarkReadAsync(NotificationId id, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); MarkedRead.Add(id); var item = Active.Single(x => x.NotificationId == id) with { State = NotificationState.Read }; return Task.FromResult(item); }
        public Task<NotificationRecord> PublishOrRefreshAsync(NotificationPublishRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NotificationRecord> ResolveAsync(NotificationId id, CancellationToken cancellationToken) { ResolvedIds.Add(id); return Task.FromResult(Active.Single(x => x.NotificationId == id)); }
        public Task<int> PurgeExpiredResolvedAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
