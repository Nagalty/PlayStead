using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.UI.Home;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Home;

public sealed class HomeMediaIntegrationTests
{
    [Fact]
    public void Hero_surface_binds_artwork_and_preserves_static_fallback()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
            "src", "PlayStead.UI", "Home", "HomeView.xaml")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var document = System.Xml.Linq.XDocument.Load(Path.Combine(directory.FullName,
            "src", "PlayStead.UI", "Home", "HomeView.xaml"));
        var hero = Assert.Single(document.Descendants(), element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "HomeHero"));
        var brush = Assert.Single(hero.Descendants(), element => element.Name.LocalName == "ImageBrush");
        Assert.Equal("{Binding HeroPath}", (string?)brush.Attribute("ImageSource"));
        Assert.Equal("UniformToFill", (string?)brush.Attribute("Stretch"));
        Assert.Contains(hero.Descendants(), element => element.Name.LocalName == "DataTrigger"
            && (string?)element.Attribute("Binding") == "{Binding HasHero}"
            && (string?)element.Attribute("Value") == "True");
        Assert.Contains(hero.Descendants(), element => (string?)element.Attribute("Text") == "Accueil");
        Assert.Contains(hero.Descendants(), element => (string?)element.Attribute("Text") == "{Binding FeaturedGameTitle}");
    }

    [Fact]
    public async Task No_sessions_keeps_static_fallback_without_media_calls()
    {
        var f = new Fixture();
        var home = f.Create();
        await Refresh(home);
        Assert.Null(Value<Guid?>(home, "FeaturedGameId"));
        Assert.Null(Value<string>(home, "HeroPath"));
        Assert.False(Value<bool>(home, "HasHero"));
        Assert.Empty(f.Media.Requests);
        Assert.Empty(f.Media.CacheRequests);
    }

    [Fact]
    public async Task Active_session_selects_its_game_and_requests_Hero_with_preferred_identity()
    {
        var f = new Fixture();
        f.Store.Active = [Session(f.A, 1)];
        f.Store.Installations.Insert(0, new GameInstallation(InstallationId.New(), f.A,
            ProviderKind.Manual, "other-id", "other", null, false, true, Now));
        var home = f.Create();
        await Refresh(home);
        Assert.Equal(f.A.Value, Value<Guid?>(home, "FeaturedGameId"));
        var request = Assert.Single(f.Media.Requests);
        Assert.Equal(ProviderKind.Manual, request.Identity.Provider);
        Assert.Equal("game-a", request.Identity.ProviderGameId);
        Assert.Equal("Game A", request.Identity.CanonicalTitle);
        Assert.Equal(GameMediaAssetType.Hero, request.Type);
    }

    [Fact]
    public async Task Newest_active_start_wins_over_list_order_and_completed_session()
    {
        var f = new Fixture();
        f.Store.Active = [Session(f.A, 1), Session(f.B, 3)];
        f.Store.Recent = [Session(f.A, 4, 5)];
        var home = f.Create();
        await Refresh(home);
        Assert.Equal(f.B.Value, Value<Guid?>(home, "FeaturedGameId"));
        Assert.Equal("game-b", Assert.Single(f.Media.Requests).Identity.ProviderGameId);
    }

    [Fact]
    public async Task Latest_completed_end_wins_even_when_it_started_earlier()
    {
        var f = new Fixture();
        f.Store.Recent = [Session(f.A, 3, 4), Session(f.B, 1, 5)];
        var home = f.Create();
        await Refresh(home);
        Assert.Equal(f.B.Value, Value<Guid?>(home, "FeaturedGameId"));
    }

    [Fact]
    public async Task Cached_Hero_is_applied_without_remote_resolution()
    {
        var f = new Fixture();
        f.Store.Active = [Session(f.A, 1)];
        f.Media.CachedPath = "cached-hero.jpg";
        var home = f.Create();
        var changed = new List<string?>();
        home.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        await Refresh(home);
        Assert.Equal("cached-hero.jpg", Value<string>(home, "HeroPath"));
        Assert.True(Value<bool>(home, "HasHero"));
        Assert.Contains("HeroPath", changed);
        Assert.Contains("HasHero", changed);
        Assert.Equal(GameMediaAssetType.Hero, Assert.Single(f.Media.CacheRequests));
        Assert.Empty(f.Media.Requests);
    }

    [Fact]
    public async Task Resolver_miss_preserves_selected_game_and_fallback()
    {
        var f = new Fixture();
        f.Store.Active = [Session(f.A, 1)];
        var home = f.Create();
        await Refresh(home);
        Assert.Single(f.Media.Requests);
        Assert.Equal(f.A.Value, Value<Guid?>(home, "FeaturedGameId"));
        Assert.False(Value<bool>(home, "HasHero"));
    }

    [Theory]
    [InlineData(false, "game-a")]
    [InlineData(true, "")]
    public async Task Unusable_installation_keeps_fallback_without_resolver(bool present, string externalId)
    {
        var f = new Fixture();
        f.Store.Active = [Session(f.A, 1)];
        f.Store.Installations[0] = f.Store.Installations[0] with
            { IsPresent = present, ExternalId = externalId };
        var home = f.Create();
        await Refresh(home);
        Assert.Equal(f.A.Value, Value<Guid?>(home, "FeaturedGameId"));
        Assert.False(Value<bool>(home, "HasHero"));
        Assert.Empty(f.Media.Requests);
        Assert.Empty(f.Media.CacheRequests);
    }

    [Fact]
    public async Task Refresh_finishes_while_Hero_is_pending_then_notifies_result()
    {
        var f = new Fixture();
        f.Store.Active = [Session(f.A, 1)];
        var pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Media.Resolve = _ => pending.Task;
        var home = f.Create();
        var applied = WhenHero(home, "resolved.jpg");
        await Refresh(home).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(f.Media.Requests);
        Assert.False(pending.Task.IsCompleted);
        Assert.Equal(f.Library.Items.Count, home.LibraryGameCount);
        Assert.False(Value<bool>(home, "HasHero"));
        pending.SetResult("resolved.jpg");
        await applied.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(f.A.Value, Value<Guid?>(home, "FeaturedGameId"));
    }

    [Fact]
    public void Session_refresh_updates_featured_game_and_late_A_cannot_overwrite_B()
    {
        RunSta(() =>
        {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var f = new Fixture();
        f.Store.Active = [Session(f.A, 1)];
        var a = new TaskCompletionSource<string?>();
        var b = new TaskCompletionSource<string?>();
        f.Media.Resolve = identity => identity.ProviderGameId == "game-a" ? a.Task : b.Task;
        var home = f.Create();
        Refresh(home).GetAwaiter().GetResult();
        f.Store.Active = [Session(f.B, 2)];
        // This is the existing notification path used by live session refresh.
        f.Sessions.RefreshLive();
        Assert.Equal(f.B.Value, Value<Guid?>(home, "FeaturedGameId"));
        b.SetResult("b.jpg");
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.Equal("b.jpg", Value<string>(home, "HeroPath"));
        a.SetResult("a.jpg");
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.Equal("b.jpg", Value<string>(home, "HeroPath"));
        Assert.Equal(2, f.Media.Requests.Count);
        home.Dispose();
        });
    }

    [Fact]
    public async Task Losing_all_sessions_clears_previous_Hero()
    {
        var f = new Fixture();
        f.Store.Active = [Session(f.A, 1)];
        f.Media.CachedPath = "a.jpg";
        var home = f.Create();
        await Refresh(home);
        f.Store.Active = [];
        f.Sessions.RefreshLive();
        Assert.Null(Value<Guid?>(home, "FeaturedGameId"));
        Assert.False(Value<bool>(home, "HasHero"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_or_acquisition_error_keeps_Home_functional(bool cancelled)
    {
        var f = new Fixture();
        f.Store.Active = [Session(f.A, 1)];
        f.Media.Resolve = _ => cancelled
            ? Task.FromCanceled<string?>(new CancellationToken(true))
            : Task.FromException<string?>(new HttpRequestException("offline"));
        var home = f.Create();
        await Refresh(home);
        Assert.Single(f.Media.Requests);
        Assert.False(Value<bool>(home, "HasHero"));
    }

    [Fact]
    public void Loaded_view_starts_nonblocking_Hero_lifecycle()
    {
        RunSta(() =>
        {
            var f = new Fixture();
            f.Store.Active = [Session(f.A, 1)];
            var pending = new TaskCompletionSource<string?>();
            f.Media.Resolve = _ => pending.Task;
            var home = f.Create();
            var view = new HomeView(home);
            var window = new Window { Content = view, Width = 900, Height = 700,
                ShowActivated = false, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.True(view.IsLoaded);
                Assert.Single(f.Media.Requests);
                Assert.False(pending.Task.IsCompleted);
                Assert.False(Value<bool>(home, "HasHero"));
                pending.SetResult(null);
                view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }
            finally { window.Close(); }
        });
    }

    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    private static GameSession Session(GameId game, int start, int? end = null) =>
        new(Guid.NewGuid(), game.Value, Now.AddMinutes(start), Now.AddMinutes(end ?? start),
            end.HasValue ? Now.AddMinutes(end.Value) : null,
            end.HasValue ? SessionState.Ended : SessionState.Active,
            end.HasValue ? SessionEndReason.ProcessExited : null,
            SessionDetectionSource.ProcessMonitor, Now, Now);

    private static T? Value<T>(HomeViewModel home, string name)
    {
        var property = typeof(HomeViewModel).GetProperty(name);
        Assert.NotNull(property);
        return (T?)property.GetValue(home);
    }

    private static Task Refresh(HomeViewModel home)
    {
        var method = typeof(HomeViewModel).GetMethod("RefreshFeaturedGameAsync", [typeof(CancellationToken)]);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(home, [CancellationToken.None]));
    }

    private static Task WhenHero(HomeViewModel home, string path)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        home.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "HeroPath" && Value<string>(home, "HeroPath") == path)
                completion.TrySetResult();
        };
        return completion.Task;
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private sealed class Fixture
    {
        public GameId A { get; } = GameId.New();
        public GameId B { get; } = GameId.New();
        public Store Store { get; } = new();
        public Media Media { get; } = new();
        public SessionMonitor Monitor { get; } = new(new Runtime(), SessionMonitorOptions.Default);
        public LibraryViewModel Library { get; }
        public SessionViewModel Sessions { get; }

        public Fixture()
        {
            Store.Games = [new(A, "Game A", false, Now, Now), new(B, "Game B", false, Now, Now)];
            Store.Installations =
            [
                new(InstallationId.New(), A, ProviderKind.Manual, "game-a", "a", null, true, true, Now),
                new(InstallationId.New(), B, ProviderKind.Manual, "game-b", "b", null, true, true, Now)
            ];
            Library = new LibraryViewModel(Store);
            Sessions = new SessionViewModel(Store, Monitor, TimeProvider.System);
        }

        public HomeViewModel Create()
        {
            var constructor = typeof(HomeViewModel).GetConstructor(
                [typeof(LibraryViewModel), typeof(SessionViewModel), typeof(NavigationService),
                 typeof(ILibraryStore), typeof(ISessionStore), typeof(SessionMonitor),
                 typeof(IGameMediaResolver), typeof(ILogger<HomeViewModel>)]);
            Assert.True(constructor is not null, "Home requires generic featured-game media integration.");
            return (HomeViewModel)constructor.Invoke([Library, Sessions, new NavigationService(),
                Store, Store, Monitor, Media, NullLogger<HomeViewModel>.Instance]);
        }
    }

    private sealed class Media : IGameMediaResolver
    {
        public string? CachedPath { get; set; }
        public Func<GameMediaIdentity, Task<string?>> Resolve { get; set; } = _ => Task.FromResult<string?>(null);
        public List<(GameMediaIdentity Identity, GameMediaAssetType Type)> Requests { get; } = [];
        public List<GameMediaAssetType> CacheRequests { get; } = [];
        public string? TryGetCachedPath(GameMediaIdentity identity, GameMediaAssetType type)
        { CacheRequests.Add(type); return CachedPath; }
        public Task<string?> ResolveAndCacheAsync(GameMediaIdentity identity, GameMediaAssetType type,
            CancellationToken cancellationToken)
        { Requests.Add((identity, type)); return Resolve(identity); }
    }

    private sealed class Store : ILibraryStore, ISessionStore
    {
        public IReadOnlyList<LogicalGame> Games { get; set; } = [];
        public List<GameInstallation> Installations { get; set; } = [];
        public IReadOnlyList<GameSession> Active { get; set; } = [];
        public IReadOnlyList<GameSession> Recent { get; set; } = [];
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken token) =>
            Task.FromResult(new LibrarySnapshot(Games, Installations));
        public Task ApplySourceScanAsync(SourceScanResult result, CancellationToken token) => throw new NotSupportedException();
        public Task UpsertAsync(GameSession session, CancellationToken token) => throw new NotSupportedException();
        public Task<GameSession?> GetAsync(Guid id, CancellationToken token) => Task.FromResult<GameSession?>(null);
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken token) => Task.FromResult(Active);
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken token) => Task.FromResult(Recent);
    }

    private sealed class Runtime : ISessionRuntime
    {
        public Task<SessionRuntimeSnapshot> RefreshAsync(CancellationToken token) => Task.FromResult(new SessionRuntimeSnapshot(Now, []));
        public Task CorrectSessionAsync(SessionCorrectionRequest request, CancellationToken token) => throw new NotSupportedException();
    }
}
