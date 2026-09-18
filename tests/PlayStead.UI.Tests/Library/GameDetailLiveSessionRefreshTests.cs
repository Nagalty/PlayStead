using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailLiveSessionRefreshTests
{
    [Fact] public void CurrentGame_StartSnapshot_RefreshesDetail() => Assert.True(Exercise(false, true).ViewModel.Game.IsSessionActive);
    [Fact] public void CurrentGame_EndSnapshot_RefreshesDetail() => Assert.False(Exercise(true, false).ViewModel.Game.IsSessionActive);
    [Fact] public void CurrentGame_EndSnapshot_ReturnsCtaStateToPlay() => Assert.Null(Exercise(true, false).ViewModel.SessionStatusLabel);
    [Fact] public void CurrentGame_EndSnapshot_RefreshesActivitySummary() => Assert.Equal(1, Exercise(true, false).RefreshCount);
    [Fact] public void OtherGame_Snapshot_DoesNotMutateCurrentDetail()
    {
        var fixture = Create(false);
        fixture.ViewModel.Activate();
        Publish(fixture.Monitor, Snapshot(GameId.New()));
        Assert.False(fixture.ViewModel.Game.IsSessionActive);
        Assert.Equal(0, fixture.RefreshCount);
    }
    [Fact] public void ActivateTwice_SubscribesOnce()
    {
        var fixture = Create(false); fixture.ViewModel.Activate(); fixture.ViewModel.Activate();
        Publish(fixture.Monitor, Snapshot(fixture.GameId)); Assert.Equal(1, fixture.RefreshCount);
    }
    [Fact] public void Deactivate_Unsubscribes()
    {
        var fixture = Create(false); fixture.ViewModel.Activate(); fixture.ViewModel.Deactivate();
        Publish(fixture.Monitor, Snapshot(fixture.GameId)); Assert.Equal(0, fixture.RefreshCount);
    }
    [Fact] public void SnapshotAfterDeactivate_DoesNothing() => Deactivate_Unsubscribes();

    private static Fixture Exercise(bool initial, bool active)
    {
        var f = Create(initial); f.ViewModel.Activate(); Publish(f.Monitor, active ? Snapshot(f.GameId) : Snapshot()); return f;
    }

    private static Fixture Create(bool active)
    {
        var id = GameId.New();
        var monitor = new SessionMonitor(new NoopRuntime(), SessionMonitorOptions.Default, (_, _) => Task.CompletedTask, NullLogger<SessionMonitor>.Instance);
        var count = 0;
        var vm = new GameDetailViewModel(Item(id, active), null, null, null, monitor, () => { count++; return Task.CompletedTask; });
        return new Fixture(id, monitor, vm, () => count);
    }

    private static LibraryItemViewModel Item(GameId id, bool active) => new(id, "Game", ProviderKind.Steam, "Steam", @"C:\Game", 1, SteamUpdateState.UpToDate, IsSessionActive: active);
    private static SessionRuntimeSnapshot Snapshot(params GameId[] ids) => new(DateTimeOffset.UtcNow, ids.Select(id => new GameSession(Guid.NewGuid(), id.Value, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, SessionState.Active, null, SessionDetectionSource.ProcessMonitor, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)).ToArray());
    private static void Publish(SessionMonitor monitor, SessionRuntimeSnapshot snapshot)
    {
        var publish = (Action<SessionRuntimeSnapshot>?)typeof(SessionMonitor)
            .GetField("SnapshotUpdated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(monitor);
        publish?.Invoke(snapshot);
    }
    private sealed record Fixture(GameId GameId, SessionMonitor Monitor, GameDetailViewModel ViewModel, Func<int> Count) { public int RefreshCount => Count(); }
    private sealed class NoopRuntime : ISessionRuntime { public Task<SessionRuntimeSnapshot> RefreshAsync(CancellationToken cancellationToken) => Task.FromResult(Snapshot()); public Task CorrectSessionAsync(SessionCorrectionRequest correction, CancellationToken cancellationToken) => Task.FromException(new NotSupportedException()); }
}
