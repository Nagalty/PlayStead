using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PlayStead.Core.Home;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.Core.Shortlist;
using PlayStead.Core.Notifications;
using PlayStead.UI.Home;
using PlayStead.UI.Launching;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;
using PlayStead.UI.Tests.TestSupport;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Home;

[Collection(PlaySteadWpfApplicationCollection.Name)]
public sealed class HomeMediaIntegrationTests
{
    [Fact]
    public void Editorial_placeholder_uses_a_fixed_banner_with_semantic_surface_fallback()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
            "src", "PlayStead.UI", "Home", "HomeView.xaml")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var document = System.Xml.Linq.XDocument.Load(Path.Combine(directory.FullName,
            "src", "PlayStead.UI", "Home", "HomeView.xaml"));
        var hero = Assert.Single(document.Descendants(), element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "IdleHeroPlaceholder"));
        Assert.Contains(hero.Descendants(), element => element.Name.LocalName == "ImageBrush"
            && (string?)element.Attribute("ImageSource") == "{Binding EditorialPlaceholderImageSource}"
            && (string?)element.Attribute("Stretch") == "UniformToFill");
        Assert.DoesNotContain(hero.Descendants(), element =>
            (string?)element.Attribute("Binding") == "{Binding HasIdleHeroImage}" ||
            (string?)element.Attribute("ImageSource") == "{Binding IdleHeroImageSource}");
        Assert.Contains(hero.Descendants(), element => (string?)element.Attribute("Text") == "Rien en cours pour l’instant.");
        Assert.Contains(hero.Descendants(), element => (string?)element.Attribute("Text") == "Prêt à replonger ?");
        Assert.Contains(hero.Descendants(), element => (string?)element.Attribute("Text") == "Lance un jeu, PlayStead s’occupe du reste.");
    }

    [Fact]
    public async Task No_active_sessions_uses_recent_completed_editorial_fallback()
    {
        var f = new Fixture();
        f.Store.Recent = [Session(f.A, 1, 2)];
        var home = f.Create();
        await Refresh(home);
        Assert.Equal(f.A.Value, Value<Guid?>(home, "FeaturedGameId"));
        Assert.Null(Value<string>(home, "HeroPath"));
        Assert.False(Value<bool>(home, "HasHero"));
        Assert.False(Value<bool>(home, "HasActiveSessionHero"));
        Assert.Equal(HomeEditorialPrimarySourceKind.RecentCompletedSession,
            Value<HomeEditorialPrimarySourceKind>(home, "PrimarySourceKind"));
        Assert.Equal("Reprendre l’aventure", Value<string>(home, "HeroEyebrow"));
        Assert.Equal("Game A", Value<string>(home, "HeroTitle"));
        Assert.StartsWith("Dernière session le ", Value<string>(home, "HeroSupportingText"));
    }

    [Fact]
    public async Task Recently_played_projection_deduplicates_games_reuses_session_labels_and_library_artwork()
    {
        var f = new Fixture();
        var games = Enumerable.Range(0, 6)
            .Select(_ => GameId.New())
            .ToArray();
        f.Store.Games = games.Select((game, index) =>
            new LogicalGame(game, $"Game {index}", false, Now, Now)).ToArray();
        f.Store.Installations = games.Select((game, index) =>
            new GameInstallation(InstallationId.New(), game, ProviderKind.Manual,
                $"game-{index}", $"install-{index}", null, true, true, Now)).ToList();
        f.Store.Recent =
        [
            Session(games[0], 9, 10),
            Session(games[1], 8, 9),
            Session(games[0], 7, 8),
            Session(games[2], 6, 7),
            Session(games[3], 5, 6),
            Session(games[4], 4, 5),
            Session(games[5], 3, 4)
        ];

        await f.Library.RefreshAsync(CancellationToken.None);
        await f.Sessions.RefreshAsync(CancellationToken.None);
        var cover = f.Library.Items.Single(item => item.GameId == games[0]);
        cover.SetCoverPath("game-0-cover.jpg");

        var home = f.Create();
        var cards = home.RecentlyPlayedGames;

        Assert.Equal(5, cards.Count);
        Assert.Equal(games.Take(5).Select(game => game.Value), cards.Select(card => card.GameId));
        Assert.Equal("Game 0", cards[0].GameTitle);
        var newestSession = f.Sessions.RecentSessions.First(session => session.GameId == games[0].Value);
        Assert.Equal(newestSession.StartedAtLabel, cards[0].StartedAtLabel);
        Assert.Equal(newestSession.DurationLabel, cards[0].DurationLabel);
        Assert.Same(cover, cards[0].LibraryItem);
        Assert.Equal("game-0-cover.jpg", cover.CoverPath);

        var changedProperties = new List<string?>();
        home.PropertyChanged += (_, eventArgs) => changedProperties.Add(eventArgs.PropertyName);
        f.Store.Recent = [Session(games[5], 11, 12), .. f.Store.Recent];
        await f.Sessions.RefreshAsync(CancellationToken.None);

        Assert.Contains(nameof(HomeViewModel.RecentlyPlayedGames), changedProperties);
        Assert.Equal(games[5].Value, home.RecentlyPlayedGames[0].GameId);
    }

    [Fact]
    public async Task Recently_played_cards_prefer_cached_Hero_then_Header_and_keep_fallback_without_media_calls()
    {
        var f = new Fixture();
        var thirdGame = GameId.New();
        f.Store.Games =
        [
            new(f.A, "Game A", false, Now, Now),
            new(f.B, "Game B", false, Now, Now),
            new(thirdGame, "Game C", false, Now, Now)
        ];
        f.Store.Installations.Add(new GameInstallation(InstallationId.New(), thirdGame,
            ProviderKind.Manual, "game-c", "install-c", null, true, true, Now));
        f.Store.Recent =
        [
            Session(f.A, 5, 6),
            Session(f.B, 4, 5),
            Session(thirdGame, 3, 4)
        ];
        f.Media.CachedPaths[("game-a", GameMediaAssetType.Hero)] = "game-a-hero.jpg";
        f.Media.CachedPaths[("game-b", GameMediaAssetType.Header)] = "game-b-header.jpg";

        await f.Library.RefreshAsync(CancellationToken.None);
        await f.Sessions.RefreshAsync(CancellationToken.None);
        var home = f.Create();
        await Refresh(home);

        var cards = home.RecentlyPlayedGames;
        Assert.Equal("game-a-hero.jpg", cards[0].LandscapeMediaPath);
        Assert.Equal("game-b-header.jpg", cards[1].LandscapeMediaPath);
        Assert.Null(cards[2].LandscapeMediaPath);
        Assert.True(cards[0].HasLandscapeMedia);
        Assert.False(cards[2].HasLandscapeMedia);
        Assert.Empty(f.Media.Requests);
    }

    [Fact]
    public async Task Recently_played_play_action_uses_shared_launch_service_for_exact_game()
    {
        var f = new Fixture();
        f.Store.Installations =
        [
            new(InstallationId.New(), f.A, ProviderKind.Steam, "111222", "C:\\Steam\\A", null, true, true, Now),
            new(InstallationId.New(), f.B, ProviderKind.Steam, "333444", "C:\\Steam\\B", null, true, true, Now)
        ];
        await f.Library.RefreshAsync(CancellationToken.None);
        var launcher = new RecordingLauncher();
        var service = new GameLaunchService(launcher);
        var home = f.CreateWithLaunchService(service);

        var command = FindCommand(home, "PlayRecentlyPlayedCommand");
        Assert.True(command.CanExecute(f.A.Value));
        command.Execute(f.A.Value);

        Assert.Equal("steam://rungameid/111222", launcher.Last?.ToString());
    }

    [Fact]
    public async Task Recently_played_play_action_is_disabled_when_game_cannot_launch()
    {
        var f = new Fixture();
        await f.Library.RefreshAsync(CancellationToken.None);
        var home = f.CreateWithLaunchService(new GameLaunchService(new RecordingLauncher()));

        var command = FindCommand(home, "PlayRecentlyPlayedCommand");

        Assert.False(command.CanExecute(f.A.Value));
    }

    [Fact]
    public void Recently_played_info_action_navigates_to_exact_game_detail()
    {
        var f = new Fixture();
        var navigation = new NavigationService();
        var home = f.CreateWithLaunchService(new GameLaunchService(new RecordingLauncher()), navigation);

        var command = FindCommand(home, "OpenRecentlyPlayedDetailsCommand");
        command.Execute(f.B.Value);

        Assert.Equal(AppRoute.GameDetail, navigation.CurrentRoute);
        Assert.Equal(new GameId(f.B.Value), navigation.CurrentParameter);
    }

    private static System.Windows.Input.ICommand FindCommand(HomeViewModel home, string propertyName)
    {
        var property = typeof(HomeViewModel).GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<System.Windows.Input.ICommand>(property!.GetValue(home));
    }

    private sealed class RecordingLauncher : IExternalUriLauncher
    {
        public Uri? Last { get; private set; }
        public void Open(Uri uri) => Last = uri;
    }

    [Fact]
    public async Task Editorial_placeholder_banner_is_fixed_across_refreshes()
    {
        var f = new Fixture();
        var home = f.Create();

        var path = Value<string>(home, "EditorialPlaceholderAssetPath");
        var image = Value<ImageSource>(home, "EditorialPlaceholderImageSource");
        await Refresh(home);

        Assert.Equal("Assets/Home/HomeHeroIdle01.png", path);
        Assert.NotNull(image);
        Assert.Equal(path, Value<string>(home, "EditorialPlaceholderAssetPath"));
        Assert.Same(image, Value<ImageSource>(home, "EditorialPlaceholderImageSource"));
    }

    [Fact]
    public async Task Active_session_block_is_independent_from_editorial_primary_and_uses_safe_detail_action()
    {
        var f = new Fixture();
        f.Store.Installations =
        [
            new(InstallationId.New(), f.A, ProviderKind.Steam, "111222", "C:\\Steam\\A", null, true, true, Now),
            new(InstallationId.New(), f.B, ProviderKind.Steam, "333444", "C:\\Steam\\B", null, true, true, Now)
        ];
        f.Store.Active = [Session(f.A, 1)];
        f.Shortlist.Entries = [new(f.B, 0, Now)];
        await f.Library.RefreshAsync(CancellationToken.None);
        var navigation = new NavigationService();
        var launcher = new RecordingLauncher();
        var home = f.CreateWithLaunchService(new GameLaunchService(launcher), navigation);

        await Refresh(home);

        Assert.True(Value<bool>(home, "HasActiveSessionHero"));
        Assert.Equal("L’aventure continue", Value<string>(home, "ActiveSessionEyebrow"));
        Assert.Equal("Game A", Value<string>(home, "ActiveSessionTitle"));
        Assert.Equal($"Démarré à {Now.AddMinutes(1).ToLocalTime().ToString("HH'h'mm")}",
            Value<string>(home, "ActiveSessionSupportingText"));
        Assert.Equal(HomeEditorialPrimarySourceKind.GamesDuMoment,
            Value<HomeEditorialPrimarySourceKind>(home, "PrimarySourceKind"));
        Assert.Equal("Game B", Value<string>(home, "EditorialTitle"));

        RunSta(() =>
        {
            var view = new HomeView(home);
            Arrange(view, 1200);
            Assert.Equal(Visibility.Visible,
                Assert.IsType<System.Windows.Controls.Border>(view.FindName("ActiveSessionHero")).Visibility);
        });

        FindCommand(home, "OpenActiveSessionDetailsCommand").Execute(null);

        Assert.Equal(AppRoute.GameDetail, navigation.CurrentRoute);
        Assert.Equal(f.A, navigation.CurrentParameter);
        Assert.Null(launcher.Last);
    }

    [Fact]
    public async Task Editorial_shortlist_capability_does_not_consume_games_from_its_independent_section()
    {
        var f = new Fixture();
        f.Shortlist.Entries = [new(f.A, 0, Now), new(f.B, 1, Now.AddMinutes(1))];
        var persisted = f.Shortlist.Entries.ToArray();
        f.Media.CachedPath = "editorial.jpg";
        var home = f.Create();

        await Refresh(home);

        Assert.Equal(HomeEditorialPrimarySourceKind.GamesDuMoment,
            Value<HomeEditorialPrimarySourceKind>(home, "PrimarySourceKind"));
        Assert.Equal("Jeu du moment", Value<string>(home, "EditorialEyebrow"));
        Assert.Equal("Game A", Value<string>(home, "EditorialTitle"));
        Assert.Equal("Dans tes jeux du moment", Value<string>(home, "EditorialSupportingText"));
        Assert.Equal("editorial.jpg", Value<string>(home, "HeroPath"));
        Assert.Equal([f.A.Value, f.B.Value], home.GamesDuMoment.Select(game => game.GameId));
        Assert.Equal(persisted, f.Shortlist.Entries);
    }

    [Fact]
    public async Task Active_session_collapses_editorial_placeholder_with_shortlist_and_recent_history()
    {
        var f = new Fixture();
        f.Shortlist.Entries = [new(f.A, 0, Now), new(f.B, 1, Now.AddMinutes(1))];
        f.Store.Recent = [Session(f.B, -90, -30)];
        f.Store.Active = [Session(f.A, 1)];
        await f.Sessions.RefreshAsync(CancellationToken.None);
        var home = f.Create();

        await Refresh(home);

        Assert.True(Value<bool>(home, "HasActiveSessionHero"));
        Assert.Equal([f.A.Value, f.B.Value], home.GamesDuMoment.Select(game => game.GameId));
        Assert.Single(home.RecentlyPlayedGames);

        RunSta(() =>
        {
            var view = new HomeView(home);
            Arrange(view, 1200);
            var active = Assert.IsType<System.Windows.Controls.Border>(view.FindName("ActiveSessionHero"));
            var placeholder = Assert.IsType<System.Windows.Controls.Border>(view.FindName("EditorialPlaceholder"));
            var idleHero = Assert.IsType<System.Windows.Controls.Border>(view.FindName("IdleHeroPlaceholder"));
            Assert.Equal(Visibility.Visible, active.Visibility);
            Assert.Equal(Visibility.Visible, placeholder.Visibility);
            Assert.Equal(Visibility.Collapsed, idleHero.Visibility);
        });
    }

    [Fact]
    public async Task Editorial_recent_completed_source_uses_factual_context()
    {
        var f = new Fixture();
        f.Store.Recent = [Session(f.B, -90, -30)];
        var home = f.Create();

        await Refresh(home);

        Assert.Equal(HomeEditorialPrimarySourceKind.RecentCompletedSession,
            Value<HomeEditorialPrimarySourceKind>(home, "PrimarySourceKind"));
        Assert.Equal("Reprendre l’aventure", Value<string>(home, "EditorialEyebrow"));
        Assert.Equal("Game B", Value<string>(home, "EditorialTitle"));
        Assert.StartsWith("Dernière session le ", Value<string>(home, "EditorialSupportingText"));
    }

    [Fact]
    public async Task Editorial_placeholder_is_quiet_and_has_no_game_action()
    {
        var f = new Fixture();
        var home = f.Create();

        await Refresh(home);

        Assert.Equal(HomeEditorialPrimarySourceKind.Placeholder,
            Value<HomeEditorialPrimarySourceKind>(home, "PrimarySourceKind"));
        Assert.Equal("Rien en cours pour l’instant.", Value<string>(home, "EditorialEyebrow"));
        Assert.Equal("Prêt à replonger ?", Value<string>(home, "EditorialTitle"));
        Assert.Equal("Lance un jeu, PlayStead s’occupe du reste.",
            Value<string>(home, "EditorialSupportingText"));
        Assert.False(Value<bool>(home, "HasPrimaryGame"));
        Assert.False(FindCommand(home, "PrimaryActionCommand").CanExecute(null));
    }

    [Fact]
    public async Task Editorial_refresh_exposes_dormant_game_using_injected_utc_clock()
    {
        var f = new Fixture();
        f.Shortlist.Entries = [new(f.B, 0, Now)];
        f.Store.Recent = [Session(f.A, -61 * 24 * 60, -60 * 24 * 60)];
        var constructor = typeof(HomeViewModel).GetConstructor(
            [typeof(LibraryViewModel), typeof(SessionViewModel), typeof(NavigationService),
             typeof(ILibraryStore), typeof(ISessionStore), typeof(SessionMonitor),
             typeof(IGameMediaResolver), typeof(ILogger<HomeViewModel>), typeof(GameLaunchService),
             typeof(IGamesDuMomentService), typeof(IWeeklyActivitySummaryService), typeof(IAttentionService),
             typeof(TimeProvider)]);
        Assert.NotNull(constructor);
        var home = (HomeViewModel)constructor!.Invoke(
            [f.Library, f.Sessions, new NavigationService(), f.Store, f.Store, f.Monitor,
             f.Media, NullLogger<HomeViewModel>.Instance, null, f.Shortlist, null, null,
             new FixedTimeProvider(Now)]);

        await Refresh(home);

        var dormant = Value<HomeEditorialDormantGame>(home, "DormantGame");
        Assert.NotNull(dormant);
        Assert.Equal(f.A, dormant!.Game.GameId);
        Assert.Equal(60, dormant.DaysSinceLastPlayed);
    }

    [Fact]
    public async Task Dormant_editorial_projection_exposes_factual_copy_cached_landscape_media_and_detail_navigation()
    {
        var f = new Fixture();
        f.Shortlist.Entries = [new(f.B, 0, Now)];
        f.Store.Games = [new(f.A, "Outriders", false, Now, Now), new(f.B, "Game B", false, Now, Now)];
        f.Store.Recent = [Session(f.A, -61 * 24 * 60, -60 * 24 * 60)];
        f.Media.CachedPaths[("game-a", GameMediaAssetType.Hero)] = "dormant-hero.jpg";
        var navigation = new NavigationService();
        var home = f.CreateWithLaunchService(
            new GameLaunchService(new RecordingLauncher()),
            navigation,
            new FixedTimeProvider(Now));

        await Refresh(home);

        Assert.True(Value<bool>(home, "HasDormantGame"));
        Assert.Equal("Outriders", Value<string>(home, "DormantGameTitle"));
        Assert.Equal("Ça fait 60 jours que celui-là n’a pas tourné.", Value<string>(home, "DormantGameContext"));
        Assert.Equal("dormant-hero.jpg", Value<string>(home, "DormantGameMediaPath"));

        FindCommand(home, "OpenDormantGameDetailsCommand").Execute(null);
        Assert.Equal(AppRoute.GameDetail, navigation.CurrentRoute);
        Assert.Equal(f.A, navigation.CurrentParameter);
    }

    [Fact]
    public async Task Missing_dormant_editorial_projection_exposes_no_secondary_card_state()
    {
        var f = new Fixture();
        var home = f.Create();

        await Refresh(home);

        Assert.False(Value<bool>(home, "HasDormantGame"));
        Assert.Null(Value<string>(home, "DormantGameTitle"));
        Assert.Null(Value<string>(home, "DormantGameContext"));
        Assert.Null(Value<string>(home, "DormantGameMediaPath"));
        Assert.False(FindCommand(home, "OpenDormantGameDetailsCommand").CanExecute(null));

        RunSta(() =>
        {
            var view = new HomeView(home);
            Arrange(view, 1200);
            var card = Assert.IsType<System.Windows.Controls.Border>(view.FindName("DormantGameCard"));
            var hero = Assert.IsType<System.Windows.Controls.Border>(view.FindName("EditorialPlaceholder"));
            Assert.Equal(Visibility.Collapsed, card.Visibility);
            Assert.Equal(1, System.Windows.Controls.Grid.GetColumnSpan(hero));
        });
    }

    [Fact]
    public async Task Dormant_secondary_card_keeps_shared_surface_when_landscape_media_is_missing()
    {
        var f = new Fixture();
        f.Shortlist.Entries = [new(f.B, 0, Now)];
        f.Store.Recent = [Session(f.A, -61 * 24 * 60, -60 * 24 * 60)];
        var home = f.CreateWithLaunchService(
            new GameLaunchService(new RecordingLauncher()),
            timeProvider: new FixedTimeProvider(Now));

        await Refresh(home);

        Assert.True(Value<bool>(home, "HasDormantGame"));
        Assert.Null(Value<string>(home, "DormantGameMediaPath"));
        RunSta(() =>
        {
            var view = new HomeView(home);
            Arrange(view, 1200);
            var card = Assert.IsType<System.Windows.Controls.Border>(view.FindName("DormantGameCard"));
            var media = Assert.IsType<System.Windows.Controls.Border>(view.FindName("DormantGameMedia"));
            Assert.Equal(Visibility.Visible, card.Visibility);
            var brush = Assert.IsType<System.Windows.Media.ImageBrush>(media.Background);
            Assert.Null(brush.ImageSource);
            Assert.NotNull(media);
        });
    }

    [Fact]
    public async Task Editorial_region_uses_dominant_columns_then_stacks_at_narrow_width()
    {
        var f = new Fixture();
        f.Shortlist.Entries = [new(f.B, 0, Now)];
        f.Store.Recent = [Session(f.A, -61 * 24 * 60, -60 * 24 * 60)];
        var home = f.CreateWithLaunchService(
            new GameLaunchService(new RecordingLauncher()),
            timeProvider: new FixedTimeProvider(Now));
        await Refresh(home);

        RunSta(() =>
        {
            var view = new HomeView(home);
            var region = Assert.IsType<System.Windows.Controls.Grid>(view.FindName("HomeEditorialRegion"));
            var hero = Assert.IsType<System.Windows.Controls.Border>(view.FindName("EditorialPlaceholder"));
            var card = Assert.IsType<System.Windows.Controls.Border>(view.FindName("DormantGameCard"));

            Arrange(view, 1200);
            Assert.Equal(new GridLength(3, GridUnitType.Star), region.ColumnDefinitions[0].Width);
            Assert.Equal(new GridLength(16), region.ColumnDefinitions[1].Width);
            Assert.Equal(new GridLength(2, GridUnitType.Star), region.ColumnDefinitions[2].Width);
            Assert.Equal(0, System.Windows.Controls.Grid.GetColumn(hero));
            Assert.Equal(2, System.Windows.Controls.Grid.GetColumn(card));
            Assert.Equal(0, System.Windows.Controls.Grid.GetRow(card));

            Arrange(view, 900);
            Assert.Equal(new GridLength(1, GridUnitType.Star), region.ColumnDefinitions[0].Width);
            Assert.Equal(new GridLength(0), region.ColumnDefinitions[1].Width);
            Assert.Equal(new GridLength(0), region.ColumnDefinitions[2].Width);
            Assert.Equal(0, System.Windows.Controls.Grid.GetColumn(card));
            Assert.Equal(1, System.Windows.Controls.Grid.GetRow(card));
        });
    }

    [Fact]
    public async Task Low_data_Home_shows_actionable_shortlist_and_compact_history_empty_states()
    {
        var f = new Fixture();
        var navigation = new NavigationService();
        var home = f.CreateWithLaunchService(new GameLaunchService(new RecordingLauncher()), navigation);

        await Refresh(home);

        Assert.True(Value<bool>(home, "HasNoGamesDuMoment"));
        Assert.True(Value<bool>(home, "HasNoRecentlyPlayedGames"));
        FindCommand(home, "NavigateLibraryCommand").Execute(null);
        Assert.Equal(AppRoute.Library, navigation.CurrentRoute);

        RunSta(() =>
        {
            var view = new HomeView(home);
            Arrange(view, 1200);
            Assert.Equal(Visibility.Visible,
                Assert.IsType<System.Windows.Controls.Border>(view.FindName("GamesDuMomentOnboarding")).Visibility);
            Assert.Equal(Visibility.Collapsed,
                Assert.IsType<System.Windows.Controls.ItemsControl>(view.FindName("GamesDuMomentList")).Visibility);
            Assert.Equal(Visibility.Visible,
                Assert.IsType<System.Windows.Controls.Border>(view.FindName("RecentlyPlayedEmptyState")).Visibility);
            Assert.Equal(Visibility.Collapsed,
                Assert.IsType<System.Windows.Controls.ItemsControl>(view.FindName("RecentlyPlayedGamesList")).Visibility);
            Assert.Null(view.FindName("WeeklyActivitySection"));
            Assert.Equal(Visibility.Visible,
                Assert.IsType<System.Windows.Controls.StackPanel>(view.FindName("HomeAttentionSection")).Visibility);
            Assert.Equal(Visibility.Visible,
                Assert.IsType<System.Windows.Controls.Grid>(view.FindName("HomeAttentionEmptyState")).Visibility);
            Assert.Equal(Visibility.Collapsed,
                Assert.IsType<System.Windows.Controls.Border>(view.FindName("ActiveSessionHero")).Visibility);
            Assert.Equal(Visibility.Visible,
                Assert.IsType<System.Windows.Controls.Border>(view.FindName("IdleHeroPlaceholder")).Visibility);
        });
    }

    [Fact]
    public async Task Populated_Home_hides_low_data_placeholders_without_changing_content_lists()
    {
        var f = new Fixture();
        f.Shortlist.Entries = [new(f.A, 0, Now), new(f.B, 1, Now.AddMinutes(1))];
        f.Store.Recent = [Session(f.B, 1, 2)];
        await f.Library.RefreshAsync(CancellationToken.None);
        await f.Sessions.RefreshAsync(CancellationToken.None);
        var home = f.Create();

        await Refresh(home);

        Assert.False(Value<bool>(home, "HasNoGamesDuMoment"));
        Assert.False(Value<bool>(home, "HasNoRecentlyPlayedGames"));
        Assert.Equal(2, home.GamesDuMoment.Count);
        Assert.Single(home.RecentlyPlayedGames);

        RunSta(() =>
        {
            var view = new HomeView(home);
            Arrange(view, 1200);
            Assert.Equal(Visibility.Collapsed,
                Assert.IsType<System.Windows.Controls.Border>(view.FindName("GamesDuMomentOnboarding")).Visibility);
            Assert.Equal(Visibility.Visible,
                Assert.IsType<System.Windows.Controls.ItemsControl>(view.FindName("GamesDuMomentList")).Visibility);
            Assert.Equal(Visibility.Collapsed,
                Assert.IsType<System.Windows.Controls.Border>(view.FindName("RecentlyPlayedEmptyState")).Visibility);
            Assert.Equal(Visibility.Visible,
                Assert.IsType<System.Windows.Controls.ItemsControl>(view.FindName("RecentlyPlayedGamesList")).Visibility);
        });
    }

    [Fact]
    public async Task Editorial_launch_action_and_secondary_detail_navigation_reuse_existing_services()
    {
        var f = new Fixture();
        f.Store.Installations =
        [
            new(InstallationId.New(), f.A, ProviderKind.Steam, "111222", "C:\\Steam\\A", null, true, true, Now)
        ];
        f.Shortlist.Entries = [new(f.A, 0, Now)];
        await f.Library.RefreshAsync(CancellationToken.None);
        var navigation = new NavigationService();
        var launcher = new RecordingLauncher();
        var home = f.CreateWithLaunchService(new GameLaunchService(launcher), navigation);

        await Refresh(home);

        Assert.True(Value<bool>(home, "CanLaunchPrimary"));
        Assert.Equal("Jouer", Value<string>(home, "PrimaryActionLabel"));
        Assert.True(Value<bool>(home, "HasPrimarySecondaryAction"));

        FindCommand(home, "PrimaryActionCommand").Execute(null);
        Assert.Equal("steam://rungameid/111222", launcher.Last?.ToString());

        FindCommand(home, "OpenPrimaryDetailsCommand").Execute(null);
        Assert.Equal(AppRoute.GameDetail, navigation.CurrentRoute);
        Assert.Equal(f.A, navigation.CurrentParameter);
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
        Assert.Equal(f.A.Value, Value<Guid?>(home, "ActiveSessionGameId"));
        Assert.True(Value<bool>(home, "HasActiveSessionHero"));
        Assert.Equal("L’aventure continue", Value<string>(home, "ActiveSessionEyebrow"));
        Assert.Equal("Game A", Value<string>(home, "ActiveSessionTitle"));
        Assert.Equal($"Démarré à {Now.AddMinutes(1).ToLocalTime().ToString("HH'h'mm")}",
            Value<string>(home, "ActiveSessionSupportingText"));
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
        Assert.Equal(f.B.Value, Value<Guid?>(home, "ActiveSessionGameId"));
        Assert.Contains(f.Media.Requests, request => request.Identity.ProviderGameId == "game-b");
        Assert.Contains(f.Media.Requests, request => request.Identity.ProviderGameId == "game-a");
    }

    [Fact]
    public async Task Latest_completed_end_wins_even_when_it_started_earlier()
    {
        var f = new Fixture();
        f.Store.Recent = [Session(f.A, 3, 4), Session(f.B, 1, 5)];
        var home = f.Create();
        await Refresh(home);
        Assert.Equal(f.B.Value, Value<Guid?>(home, "FeaturedGameId"));
        Assert.False(Value<bool>(home, "HasActiveSessionHero"));
        Assert.Equal(HomeEditorialPrimarySourceKind.RecentCompletedSession,
            Value<HomeEditorialPrimarySourceKind>(home, "PrimarySourceKind"));
        Assert.Equal("Game B", Value<string>(home, "HeroTitle"));
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
        Assert.Equal("cached-hero.jpg", Value<string>(home, "ActiveSessionHeroPath"));
        Assert.True(Value<bool>(home, "HasActiveSessionHeroMedia"));
        Assert.Contains("ActiveSessionHeroPath", changed);
        Assert.Contains("HasActiveSessionHeroMedia", changed);
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
        Assert.Equal(f.A.Value, Value<Guid?>(home, "ActiveSessionGameId"));
        Assert.False(Value<bool>(home, "HasActiveSessionHeroMedia"));
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
        Assert.Equal(present ? f.A.Value : (Guid?)null,
            Value<Guid?>(home, "ActiveSessionGameId"));
        Assert.Equal(HomeEditorialPrimarySourceKind.Placeholder,
            Value<HomeEditorialPrimarySourceKind>(home, "PrimarySourceKind"));
        Assert.False(Value<bool>(home, "HasActiveSessionHeroMedia"));
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
        var applied = WhenActiveHero(home, "resolved.jpg");
        await Refresh(home).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(f.Media.Requests);
        Assert.False(pending.Task.IsCompleted);
        Assert.Equal(f.Library.Items.Count, home.LibraryGameCount);
        Assert.False(Value<bool>(home, "HasActiveSessionHeroMedia"));
        pending.SetResult("resolved.jpg");
        await applied.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(f.A.Value, Value<Guid?>(home, "ActiveSessionGameId"));
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
        var changed = new List<string?>();
        home.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);
        Refresh(home).GetAwaiter().GetResult();
        f.Store.Active = [Session(f.B, 2)];
        // This is the existing notification path used by live session refresh.
        f.Sessions.RefreshLive();
        Assert.Equal(f.B.Value, Value<Guid?>(home, "ActiveSessionGameId"));
        Assert.Equal("Game B", Value<string>(home, "ActiveSessionTitle"));
        Assert.Contains("ActiveSessionTitle", changed);
        b.SetResult("b.jpg");
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.Equal("b.jpg", Value<string>(home, "ActiveSessionHeroPath"));
        a.SetResult("a.jpg");
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.Equal("b.jpg", Value<string>(home, "ActiveSessionHeroPath"));
        Assert.Equal(2, f.Media.Requests.Count);
        home.Dispose();
        });
    }

    [Fact]
    public void Completed_session_refreshes_home_recent_activity_once_on_ui_dispatcher()
    {
        RunSta(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var f = new Fixture();
            f.Store.Games =
            [
                new(f.A, "Enshrouded", false, Now, Now),
                new(f.B, "Incursion: Red River", false, Now, Now)
            ];
            var enshrouded = Session(f.A, 1, 2);
            f.Store.UpsertAsync(enshrouded, CancellationToken.None).GetAwaiter().GetResult();
            var home = f.Create();
            f.Sessions.RefreshAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.Equal(f.A.Value, Assert.Single(home.RecentSessions).GameId);
            var uiThread = Environment.CurrentManagedThreadId;
            var recentActivityThread = 0;
            home.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(HomeViewModel.RecentSessions))
                    recentActivityThread = Environment.CurrentManagedThreadId;
            };

            var started = Session(f.B, 3);
            f.Store.UpsertAsync(started, CancellationToken.None).GetAwaiter().GetResult();
            Task.Run(() => Publish(f.Monitor, new SessionRuntimeSnapshot(Now.AddMinutes(3), [started])))
                .GetAwaiter().GetResult();
            Assert.Equal(1, f.Store.RecentReadCount);
            Assert.Equal(f.A.Value, Assert.Single(home.RecentSessions).GameId);

            var ended = started with
            {
                LastSeenAtUtc = Now.AddMinutes(5),
                ObservedEndedAtUtc = Now.AddMinutes(5),
                State = SessionState.Ended,
                EndReason = SessionEndReason.ProcessExited
            };
            f.Store.UpsertAsync(ended, CancellationToken.None).GetAwaiter().GetResult();
            Task.Run(() => Publish(f.Monitor, new SessionRuntimeSnapshot(Now.AddMinutes(5), [])))
                .GetAwaiter().GetResult();
            DrainDispatcherUntil(() => home.RecentSessions.Count == 2 &&
                home.RecentSessions[0].GameId == f.B.Value);

            Assert.Equal([f.B.Value, f.A.Value], home.RecentSessions.Select(item => item.GameId));
            Assert.Equal("Incursion: Red River", home.RecentSessions[0].Title);
            Assert.Equal(uiThread, recentActivityThread);
            Assert.Equal(2, f.Store.RecentReadCount);

            Task.Run(() => Publish(f.Monitor, new SessionRuntimeSnapshot(Now.AddMinutes(6), [])))
                .GetAwaiter().GetResult();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Equal(2, f.Store.RecentReadCount);
            Assert.Equal(2, home.RecentSessions.Count);
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
        Assert.False(Value<bool>(home, "HasActiveSessionHero"));
        Assert.Equal("Rien en cours pour l’instant.", Value<string>(home, "HeroEyebrow"));
        Assert.Equal("Prêt à replonger ?", Value<string>(home, "HeroTitle"));
    }

    [Fact]
    public async Task Home_Hero_switches_from_inactive_placeholder_to_current_active_game()
    {
        var f = new Fixture();
        f.Store.Recent = [Session(f.B, 1, 2)];
        var home = f.Create();
        await Refresh(home);
        Assert.False(Value<bool>(home, "HasActiveSessionHero"));
        Assert.Equal(HomeEditorialPrimarySourceKind.RecentCompletedSession,
            Value<HomeEditorialPrimarySourceKind>(home, "PrimarySourceKind"));
        Assert.Equal("Game B", Value<string>(home, "HeroTitle"));

        var changed = new List<string?>();
        home.PropertyChanged += (_, eventArgs) => changed.Add(eventArgs.PropertyName);
        f.Store.Active = [Session(f.A, 3)];
        f.Sessions.RefreshLive();

        Assert.True(Value<bool>(home, "HasActiveSessionHero"));
        Assert.Equal(f.A.Value, Value<Guid?>(home, "ActiveSessionGameId"));
        Assert.Equal("L’aventure continue", Value<string>(home, "ActiveSessionEyebrow"));
        Assert.Equal("Game A", Value<string>(home, "ActiveSessionTitle"));
        Assert.Equal($"Démarré à {Now.AddMinutes(3).ToLocalTime().ToString("HH'h'mm")}",
            Value<string>(home, "ActiveSessionSupportingText"));
        Assert.Contains(nameof(HomeViewModel.ActiveSessionTitle), changed);
        Assert.Contains(nameof(HomeViewModel.ActiveSessionSupportingText), changed);
    }

    [Fact]
    public async Task Home_Hero_updates_when_monitor_publishes_new_active_snapshot()
    {
        var f = new Fixture();
        var home = f.Create();
        await Refresh(home);
        Assert.False(Value<bool>(home, "HasActiveSessionHero"));

        var snapshot = new SessionRuntimeSnapshot(
            Now.AddMinutes(3),
            [Session(f.A, 3)]);
        typeof(SessionMonitor).GetProperty(
            nameof(SessionMonitor.LatestSnapshot))!
            .SetValue(f.Monitor, snapshot);
        Publish(f.Monitor, snapshot);

        await Task.Yield();
        Assert.True(Value<bool>(home, "HasActiveSessionHero"));
        Assert.Equal(f.A.Value, Value<Guid?>(home, "ActiveSessionGameId"));
        Assert.Equal(HomeEditorialPrimarySourceKind.Placeholder,
            Value<HomeEditorialPrimarySourceKind>(home, "PrimarySourceKind"));
    }

    [Fact]
    public void Idle_Hero_pool_contains_and_loads_exactly_four_packaged_assets()
    {
        RunSta(() =>
        {
            var poolType = typeof(HomeViewModel).Assembly.GetType(
                "PlayStead.UI.Home.HomeHeroIdlePlaceholderPool");
            Assert.NotNull(poolType);
            var pathsProperty = poolType.GetProperty("ResourcePaths",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(pathsProperty);
            var paths = Assert.IsAssignableFrom<IReadOnlyList<string>>(pathsProperty.GetValue(null));
            Assert.Equal(4, paths.Count);
            Assert.Equal(new[]
            {
                "Assets/Home/HomeHeroIdle01.png", "Assets/Home/HomeHeroIdle02.png",
                "Assets/Home/HomeHeroIdle03.png", "Assets/Home/HomeHeroIdle04.png"
            }, paths);

            var load = poolType.GetMethod("TryLoad", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(load);
            foreach (var path in paths)
            {
                Assert.IsAssignableFrom<ImageSource>(load.Invoke(null, [path]));
            }
        });
    }

    [Fact]
    public void Home_Hero_selects_idle_once_and_reuses_it_after_active_session()
    {
        RunSta(() =>
        {
            var f = new Fixture();
            f.Store.Recent = [Session(f.B, 1, 2)];
            f.Media.CachedPath = "active-game-hero.jpg";
            var home = f.Create();
            var selectedPath = Value<string>(home, "IdleHeroAssetPath");
            var selectedImage = Value<ImageSource>(home, "IdleHeroImageSource");

            Assert.NotNull(selectedPath);
            Assert.NotNull(selectedImage);
            Assert.Contains(selectedPath, IdleHeroResourcePaths());

            Refresh(home).GetAwaiter().GetResult();
            Assert.Equal(selectedPath, Value<string>(home, "IdleHeroAssetPath"));
            Assert.Same(selectedImage, Value<ImageSource>(home, "IdleHeroImageSource"));

            f.Store.Active = [Session(f.A, 3)];
            f.Sessions.RefreshLive();
            Assert.True(Value<bool>(home, "HasActiveSessionHero"));
            Assert.Equal("active-game-hero.jpg", Value<string>(home, "ActiveSessionHeroPath"));
            Assert.Equal(selectedPath, Value<string>(home, "IdleHeroAssetPath"));

            f.Store.Active = [];
            f.Sessions.RefreshLive();
            Assert.False(Value<bool>(home, "HasActiveSessionHero"));
            Assert.Equal(selectedPath, Value<string>(home, "IdleHeroAssetPath"));
            Assert.Same(selectedImage, Value<ImageSource>(home, "IdleHeroImageSource"));
        });
    }

    [Fact]
    public void Missing_idle_hero_asset_falls_back_without_throwing()
    {
        RunSta(() =>
        {
            var poolType = typeof(HomeViewModel).Assembly.GetType(
                "PlayStead.UI.Home.HomeHeroIdlePlaceholderPool");
            Assert.NotNull(poolType);
            var load = poolType.GetMethod("TryLoad", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(load);
            var result = Record.Exception(() => load.Invoke(null, ["Assets/Home/does-not-exist.png"]));
            Assert.Null(result);
            Assert.Null(load.Invoke(null, ["Assets/Home/does-not-exist.png"]));
        });
    }

    private static IReadOnlyList<string> IdleHeroResourcePaths()
    {
        var poolType = typeof(HomeViewModel).Assembly.GetType(
            "PlayStead.UI.Home.HomeHeroIdlePlaceholderPool");
        Assert.NotNull(poolType);
        var property = poolType.GetProperty("ResourcePaths",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(property.GetValue(null));
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
            var app = Application.Current!;

            Assert.NotNull(app.TryFindResource("PlayStead.Icon.Gamepad"));
            Assert.NotNull(app.TryFindResource("PlayStead.Icon.History"));

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
                PlaySteadWpfTestResources.ShowAndPumpLoaded(window, view);
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

    private static Task WhenActiveHero(HomeViewModel home, string path)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        home.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "ActiveSessionHeroPath" &&
                Value<string>(home, "ActiveSessionHeroPath") == path)
                completion.TrySetResult();
        };
        return completion.Task;
    }

    private static void RunSta(Action action)
        => PlaySteadWpfTestResources.Run(action);

    private static void Arrange(FrameworkElement element, double width)
    {
        element.Measure(new Size(width, 900));
        element.Arrange(new Rect(0, 0, width, 900));
        element.UpdateLayout();
    }

    private sealed class Fixture
    {
        public GameId A { get; } = GameId.New();
        public GameId B { get; } = GameId.New();
        public Store Store { get; } = new();
        public Media Media { get; } = new();
        public ShortlistService Shortlist { get; } = new();
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
            Sessions = new SessionViewModel(Store, Monitor, TimeProvider.System, Store, new CorrectionStore(),
                new Runtime(), new SessionCorrectionPolicy());
        }

        public HomeViewModel Create()
        {
            var constructor = typeof(HomeViewModel).GetConstructor(
                [typeof(LibraryViewModel), typeof(SessionViewModel), typeof(NavigationService),
                 typeof(ILibraryStore), typeof(ISessionStore), typeof(SessionMonitor),
                 typeof(IGameMediaResolver), typeof(ILogger<HomeViewModel>), typeof(GameLaunchService),
                 typeof(IGamesDuMomentService), typeof(IWeeklyActivitySummaryService), typeof(IAttentionService),
                 typeof(TimeProvider)]);
            Assert.True(constructor is not null, "Home requires generic featured-game media integration.");
            return (HomeViewModel)constructor.Invoke([Library, Sessions, new NavigationService(),
                Store, Store, Monitor, Media, NullLogger<HomeViewModel>.Instance, null, Shortlist, null, null,
                TimeProvider.System]);
        }

        public HomeViewModel CreateWithLaunchService(
            GameLaunchService launchService,
            NavigationService? navigation = null,
            TimeProvider? timeProvider = null)
        {
            var constructor = typeof(HomeViewModel).GetConstructor(
                [typeof(LibraryViewModel), typeof(SessionViewModel), typeof(NavigationService),
                 typeof(ILibraryStore), typeof(ISessionStore), typeof(SessionMonitor),
                 typeof(IGameMediaResolver), typeof(ILogger<HomeViewModel>), typeof(GameLaunchService),
                 typeof(IGamesDuMomentService), typeof(IWeeklyActivitySummaryService), typeof(IAttentionService),
                 typeof(TimeProvider)]);
            Assert.NotNull(constructor);
            return (HomeViewModel)constructor!.Invoke([Library, Sessions, navigation ?? new NavigationService(),
                Store, Store, Monitor, Media, NullLogger<HomeViewModel>.Instance, launchService, Shortlist, null, null,
                timeProvider ?? TimeProvider.System]);
        }
    }

    private sealed class ShortlistService : IGamesDuMomentService
    {
        public IReadOnlyList<GamesDuMomentEntry> Entries { get; set; } = [];

        public Task<IReadOnlyList<GamesDuMomentEntry>> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Entries);

        public Task<GamesDuMomentAddResult> AddAsync(GameId gameId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> RemoveAsync(GameId gameId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReorderAsync(IReadOnlyList<GameId> orderedGameIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class Media : IGameMediaResolver
    {
        public string? CachedPath { get; set; }
        public Dictionary<(string ProviderGameId, GameMediaAssetType Type), string> CachedPaths { get; } = [];
        public Func<GameMediaIdentity, Task<string?>> Resolve { get; set; } = _ => Task.FromResult<string?>(null);
        public List<(GameMediaIdentity Identity, GameMediaAssetType Type)> Requests { get; } = [];
        public List<GameMediaAssetType> CacheRequests { get; } = [];
        public string? TryGetCachedPath(GameMediaIdentity identity, GameMediaAssetType type)
        {
            CacheRequests.Add(type);
            return CachedPaths.TryGetValue((identity.ProviderGameId, type), out var path)
                ? path
                : CachedPath;
        }
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
        public int RecentReadCount { get; private set; }
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken token) =>
            Task.FromResult(new LibrarySnapshot(Games, Installations));
        public Task ApplySourceScanAsync(SourceScanResult result, CancellationToken token) => throw new NotSupportedException();
        public Task UpsertAsync(GameSession session, CancellationToken token)
        {
            Active = Active.Where(item => item.SessionId != session.SessionId).ToArray();
            Recent = Recent.Where(item => item.SessionId != session.SessionId).Append(session).ToArray();
            if (session.State == SessionState.Active) Active = Active.Append(session).ToArray();
            return Task.CompletedTask;
        }
        public Task<GameSession?> GetAsync(Guid id, CancellationToken token) =>
            Task.FromResult(Recent.Concat(Active).FirstOrDefault(session => session.SessionId == id));
        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<GameSession>>(
                Recent
                    .Where(session => session.GameId == gameId)
                    .ToArray());
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken token) => Task.FromResult(Active);
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken token)
        {
            RecentReadCount++;
            return Task.FromResult<IReadOnlyList<GameSession>>(Recent.OrderByDescending(session => session.ObservedStartedAtUtc).Take(limit).ToArray());
        }
    }

    private sealed class Runtime : ISessionRuntime
    {
        public Task<SessionRuntimeSnapshot> RefreshAsync(CancellationToken token) => Task.FromResult(new SessionRuntimeSnapshot(Now, []));
        public Task CorrectSessionAsync(SessionCorrectionRequest request, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class CorrectionStore : ISessionCorrectionStore
    {
        public Task UpsertAsync(SessionCorrection correction, CancellationToken token) => Task.CompletedTask;
        public Task<SessionCorrection?> GetAsync(Guid sessionId, CancellationToken token) =>
            Task.FromResult<SessionCorrection?>(null);
    }

    private static void Publish(SessionMonitor monitor, SessionRuntimeSnapshot snapshot)
    {
        var publish = (Action<SessionRuntimeSnapshot>?)typeof(SessionMonitor)
            .GetField("SnapshotUpdated", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(monitor);
        publish?.Invoke(snapshot);
    }

    private static void DrainDispatcherUntil(Func<bool> condition)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        var deadline = DateTime.UtcNow.AddSeconds(5);
        timer.Tick += (_, _) =>
        {
            if (!condition() && DateTime.UtcNow < deadline) return;
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        dispatcher.BeginInvoke(() =>
        {
            if (!condition() && DateTime.UtcNow < deadline) return;
            timer.Stop();
            frame.Continue = false;
        });
        Dispatcher.PushFrame(frame);
        Assert.True(condition(), "The Home activity refresh did not complete.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
