using PlayStead.UI.Tests.TestSupport;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.UI.Controls;
using PlayStead.UI.Library;
using PlayStead.UI.Sessions;
using PlayStead.UI.Steam;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryViewMediaWiringTests
{
    [Fact]
    public void Visible_card_requests_cover_for_exact_item_through_library_view_model()
    {
        RunSta(() =>
        {
            var resolver = new Resolver();
            var vm = CreateLibrary(resolver);
            var item = Assert.Single(vm.Items);
            WithView(vm, (view, card) =>
            {
                Assert.Same(item, card.DataContext);
                Assert.Equal(1, resolver.Calls);
                Assert.NotNull(resolver.Identity);
                Assert.Equal(ProviderKind.Steam, resolver.Identity.Provider);
                Assert.Equal("1874880", resolver.Identity.ProviderGameId);
                Assert.Equal("Arma Reforger", resolver.Identity.CanonicalTitle);
                Assert.Equal(GameMediaAssetType.Cover, resolver.AssetType);
                Assert.Equal(Resolver.Path, item.CoverPath);
            });
        });
    }

    [Fact]
    public void Cancelled_media_request_is_non_fatal_and_keeps_cover_absent()
    {
        RunSta(() =>
        {
            var resolver = new Resolver { Cancel = true };
            var vm = CreateLibrary(resolver);
            WithView(vm, (view, card) =>
            {
                Assert.Equal(1, resolver.Calls);
                Assert.False(Assert.Single(vm.Items).HasCover);
            });
        });
    }

    [Fact]
    public void Media_request_without_library_view_model_is_ignored()
    {
        RunSta(() =>
        {
            // Resolve the handler from the actual template rather than inventing its name.
            var xaml = XDocument.Load(SourcePath("LibraryView.xaml"));
            var cardElement = Assert.Single(xaml.Descendants(),
                element => element.Name.LocalName == "GameCard");
            var handlerName = (string?)cardElement.Attribute("MediaRequested");
            Assert.False(string.IsNullOrWhiteSpace(handlerName),
                "LibraryView must wire the GameCard MediaRequested event.");
            var handler = typeof(LibraryView).GetMethod(handlerName!,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(handler);
            var view = new LibraryView();
            var item = new LibraryItemViewModel(GameId.New(), "Arma Reforger",
                ProviderKind.Steam, "Steam", @"C:\Games\Arma", null);
            foreach (var context in new object?[] { null, new object() })
            {
                view.DataContext = context;
                handler.Invoke(view, [new GameCard { DataContext = item }, item]);
                Drain(view.Dispatcher);
                Assert.False(item.HasCover);
            }
        });
    }

    [Fact]
    public void Library_view_delegates_media_resolution_without_direct_resolver_or_network()
    {
        var source = File.ReadAllText(SourcePath("LibraryView.xaml.cs"));
        Assert.Contains("EnsureCoverAsync(", source);
        Assert.DoesNotContain("ResolveAndCacheAsync", source);
        Assert.DoesNotContain("IGameMediaResolver", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("WebClient", source);
    }

    private static LibraryViewModel CreateLibrary(Resolver resolver)
    {
        var now = DateTimeOffset.UtcNow;
        var id = GameId.New();
        var snapshot = new LibrarySnapshot(
            [new LogicalGame(id, "Arma Reforger", false, now, now)],
            [new GameInstallation(InstallationId.New(), id, ProviderKind.Steam,
                "1874880", @"C:\Games\Arma", null, true, true, now)]);
        var vm = new LibraryViewModel(new StubLibraryStore(snapshot),
            new RecordingSteamReferenceRuntime(),
            new SessionMonitor(new FakeSessionRuntime(now), SessionMonitorOptions.Default),
            resolver);
        vm.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
        vm.SetViewMode(LibraryViewMode.Grid);
        Assert.Equal(0, resolver.Calls);
        return vm;
    }

    private static void WithView(LibraryViewModel vm, Action<LibraryView, GameCard> assertion)
    {
        var view = new LibraryView { DataContext = vm };
        var window = new Window { Content = view, Width = 1000, Height = 700,
            ShowInTaskbar = false, ShowActivated = false };
        var errors = new List<Exception>();
        DispatcherUnhandledExceptionEventHandler onError = (_, e) =>
        {
            errors.Add(e.Exception);
            e.Handled = true;
        };
        view.Dispatcher.UnhandledException += onError;
        try
        {
            window.Show();
            window.UpdateLayout();
            Drain(view.Dispatcher);
            var card = Assert.Single(Descendants(view).OfType<GameCard>(),
                card => card.IsVisible);
            Assert.True(card.IsLoaded);
            assertion(view, card);
            Assert.Empty(errors);
        }
        finally
        {
            window.Close();
            view.Dispatcher.UnhandledException -= onError;
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Drain(Dispatcher dispatcher) =>
        dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static string SourcePath(string file)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null; directory = directory.Parent)
        {
            var path = System.IO.Path.Combine(directory.FullName,
                "src", "PlayStead.UI", "Library", file);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException(file);
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { PlaySteadWpfTestResources.Run(action); }
            catch (Exception exception) { error = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private sealed class Resolver : IGameMediaResolver
    {
        public const string Path = @"C:\Media\cover.jpg";
        public bool Cancel { get; init; }
        public int Calls { get; private set; }
        public GameMediaIdentity? Identity { get; private set; }
        public GameMediaAssetType? AssetType { get; private set; }
        public string? TryGetCachedPath(GameMediaIdentity identity, GameMediaAssetType assetType) => null;
        public Task<string?> ResolveAndCacheAsync(GameMediaIdentity identity,
            GameMediaAssetType assetType, CancellationToken cancellationToken)
        {
            Calls++;
            Identity = identity;
            AssetType = assetType;
            return Cancel
                ? Task.FromCanceled<string?>(new CancellationToken(true))
                : Task.FromResult<string?>(Path);
        }
    }
    private sealed class RecordingSteamReferenceRuntime :
        ISteamReferenceRuntime
    {
        private static readonly SteamReferenceSnapshot Empty =
            new(Array.Empty<SteamReferenceEntry>());

        public SteamReferenceSnapshot Current { get; private set; } =
            Empty;

        public Task<SteamReferenceSnapshot> LoadCachedAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = Empty;
            return Task.FromResult(Current);
        }

        public Task<SteamReferenceSnapshot> RefreshStaleAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = Empty;
            return Task.FromResult(Current);
        }

        public Task<SteamReferenceSnapshot> RefreshAllAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current = Empty;
            return Task.FromResult(Current);
        }
    }

    private sealed class FakeSessionRuntime(
        DateTimeOffset observedAtUtc) : ISessionRuntime
    {
        public Task<SessionRuntimeSnapshot> RefreshAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new SessionRuntimeSnapshot(
                    observedAtUtc,
                    []));
        }

        public Task CorrectSessionAsync(
            SessionCorrectionRequest correction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw new InvalidOperationException(
                "Library media tests must not correct sessions.");
        }
    }

    private sealed class StubLibraryStore(
        LibrarySnapshot snapshot) : ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }
    }
}
