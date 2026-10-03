using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Steam;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.UI.Library;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;
using PlayStead.UI.Tests.TestSupport;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailMetadataProjectionTests
{
    [Fact]
    public async Task Canonical_metadata_is_projected_without_network()
    {
        var contentId = CatalogContentId.New();
        var content = new CatalogContent(contentId, PlaySteadPublicId.Parse("PlayStead-123456"), CatalogContentKind.Game, "Game", "game", new DateOnly(2020, 4, 5), "Dev", "Pub", CatalogContentStatus.Active, null);
        var item = new LibraryItemViewModel(GameId.New(), "Game", ProviderKind.Steam, "Steam", @"C:\Games\Game", null, CanonicalContentId: contentId);
        var vm = new GameDetailViewModel(item, null, null, null, new FakeCatalogStore(content));

        await vm.LoadAsync(CancellationToken.None);

        Assert.Equal("Dev", vm.DeveloperDisplay);
        Assert.Equal("Pub", vm.PublisherDisplay);
        Assert.Equal("5 avril 2020", vm.ReleaseDateDisplay);
    }

    [Fact]
    public async Task Missing_canonical_metadata_remains_unavailable()
    {
        var item = new LibraryItemViewModel(GameId.New(), "Game", ProviderKind.Steam, "Steam", @"C:\Games\Game", null);
        var vm = new GameDetailViewModel(item, null, null, null, new FakeCatalogStore(null));
        await vm.LoadAsync(CancellationToken.None);
        Assert.Null(vm.DeveloperDisplay);
        Assert.Null(vm.PublisherDisplay);
        Assert.Null(vm.ReleaseDateDisplay);
    }

    [Fact]
    public async Task Genres_are_localized_in_the_ui_without_mutating_provider_metadata()
    {
        var gameId = GameId.New();
        var sourceGenres = new[]
        {
            "Strategy",
            "Tactical",
            "Turn-based strategy (TBS)",
            "Uncatalogued Genre"
        };
        var metadata = ProviderGameMetadata.Create(
            gameId,
            ProviderKind.Manual,
            $"manual:{gameId.Value:D}",
            DateTimeOffset.UtcNow,
            genres: sourceGenres);
        var item = new LibraryItemViewModel(
            gameId,
            "007 First Light",
            ProviderKind.Manual,
            "Manuel",
            @"H:\\007 First Light",
            null);
        var vm = new GameDetailViewModel(
            item,
            null,
            null,
            null,
            providerGameMetadataStore: new FakeProviderMetadataStore(metadata));

        await vm.LoadAsync(CancellationToken.None);

        Assert.Equal(
            ["Stratégie", "Tactique", "Stratégie au tour par tour", "Uncatalogued Genre"],
            vm.Genres);
        Assert.Equal(sourceGenres, metadata.Genres);
    }

    [Fact]
    public async Task Manual_game_uses_current_linked_steam_metadata_without_changing_provider()
    {
        var gameId = GameId.New();
        var manual = ProviderGameMetadata.Create(gameId, ProviderKind.Manual, "manual:game", DateTimeOffset.UtcNow, genres: null, developers: ["Canonical Studio"]);
        var steam = ProviderGameMetadata.Create(gameId, ProviderKind.Steam, "2075800", DateTimeOffset.UtcNow, genres: ["Adventure"], publishers: ["Steam Publisher"], onlineCoop: true);
        var item = new LibraryItemViewModel(gameId, "STAR WARS Zero Company", ProviderKind.Manual, "Manuel", @"C:\\Manual", null);
        var vm = new GameDetailViewModel(
            item, null, null, null,
            providerGameMetadataStore: new FakeProviderMetadataStore(manual, steam),
            manualMetadataLinkStore: new FakeManualMetadataLinkStore(new(gameId, CatalogContentId.New(), new PlayStead.Core.Media.MediaSourceIdentity(ProviderKind.Steam, "2075800"), DateTimeOffset.UtcNow)));

        await vm.LoadAsync(CancellationToken.None);

        Assert.Equal(ProviderKind.Manual, vm.Game.Provider);
        Assert.Equal(["Aventure"], vm.Genres);
        Assert.Equal("Canonical Studio", vm.DeveloperDisplay);
        Assert.Equal("Steam Publisher", vm.PublisherDisplay);
        Assert.Equal(["Coop en ligne"], vm.GameModes);
    }

    [Fact]
    public void Genre_localizer_keeps_case_insensitivity_and_trimmed_unknown_fallback()
    {
        Assert.Equal("Tactique", GenreDisplayLocalizer.Localize("  tAcTiCaL "));
        Assert.Equal("Fantastique", GenreDisplayLocalizer.Localize("  fantasy "));
        Assert.Equal("Genre inédit", GenreDisplayLocalizer.Localize("  Genre inédit "));
    }

    [Theory]
    [InlineData(new[] { "FPP", "Tir" }, new[] { "FPS" })]
    [InlineData(new[] { "Tir", "FPP" }, new[] { "FPS" })]
    [InlineData(new[] { "FPP", "Shooter" }, new[] { "FPS" })]
    [InlineData(new[] { "TPP", "Tir" }, new[] { "TPS" })]
    [InlineData(new[] { "TPP", "Shooter" }, new[] { "TPS" })]
    [InlineData(new[] { "FPP" }, new[] { "FPP" })]
    [InlineData(new[] { "TPP" }, new[] { "TPP" })]
    [InlineData(new[] { "FPP", "Tir", "SF" }, new[] { "FPS", "Science-fiction" })]
    [InlineData(new[] { "FPP", "Tir", "Horreur" }, new[] { "FPS", "Horreur" })]
    [InlineData(new[] { "FPP", "Tir", "FPS" }, new[] { "FPS" })]
    public void Genre_localizer_composes_gog_taxonomy_without_mutating_values(string[] source, string[] expected)
    {
        Assert.Equal(expected, GenreDisplayLocalizer.LocalizeMany(source));
    }

    [Fact]
    public void Genre_localizer_composition_is_case_insensitive_trim_safe_and_order_stable()
    {
        Assert.Equal(
            ["Horreur", "FPS", "Science-fiction"],
            GenreDisplayLocalizer.LocalizeMany([" Horreur ", " FPP ", "TIR", "sf"]));
    }

    [Fact]
    public void Loaded_metadata_updates_effective_general_info_row_visibility()
    {
        RunSta(() =>
        {
            var contentId = CatalogContentId.New();
            var content = new CatalogContent(contentId, PlaySteadPublicId.Parse("PlayStead-1203620"), CatalogContentKind.Game,
                "Enshrouded", "enshrouded", null, "Keen Games GmbH", "Keen Games GmbH", CatalogContentStatus.Active, null);
            var item = new LibraryItemViewModel(GameId.New(), "Enshrouded", ProviderKind.Steam, "Steam",
                @"C:\Steam\Enshrouded", null, CanonicalContentId: contentId);
            var vm = new GameDetailViewModel(item, null, null, null, new FakeCatalogStore(content));
            var changedProperties = new HashSet<string?>();
            vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
            var view = new GameDetailView(vm) { Width = 1200, Height = 900 };

            view.Measure(new Size(1200, 900));
            view.Arrange(new Rect(0, 0, 1200, 900));
            view.UpdateLayout();
            FlushBindings(view.Dispatcher);

            // The general-info card remains present even before metadata arrives.
            vm.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            FlushBindings(view.Dispatcher);
            view.UpdateLayout();

            Assert.True(vm.HasDeveloper);
            Assert.True(vm.HasPublisher);
            Assert.False(vm.HasReleaseDate);
            Assert.True(vm.HasGeneralInfo);
            Assert.Contains(nameof(vm.DeveloperDisplay), changedProperties);
            Assert.Contains(nameof(vm.PublisherDisplay), changedProperties);
            Assert.Contains(nameof(vm.ReleaseDateDisplay), changedProperties);
            Assert.Contains(nameof(vm.HasDeveloper), changedProperties);
            Assert.Contains(nameof(vm.HasPublisher), changedProperties);
            Assert.Contains(nameof(vm.HasReleaseDate), changedProperties);
            Assert.Contains(nameof(vm.HasGeneralInfo), changedProperties);

            var developerLabel = FindText(view, "Développeur");
            var developerValue = FindByTextBinding(view, "DeveloperDisplay");
            Assert.Equal(Visibility.Visible, developerLabel.Visibility);
            Assert.Equal(Visibility.Visible, developerValue.Visibility);
            Assert.Equal("Keen Games GmbH", developerValue.Text);

            var publisherLabel = FindText(view, "Éditeur");
            var publisherValue = FindByTextBinding(view, "PublisherDisplay");
            Assert.Equal(Visibility.Visible, publisherLabel.Visibility);
            Assert.Equal(Visibility.Visible, publisherValue.Visibility);
            Assert.Equal("Keen Games GmbH", publisherValue.Text);

            Assert.Equal(Visibility.Visible, FindText(view, "Date de sortie").Visibility);
            Assert.Equal(Visibility.Visible, FindByTextBinding(view, "ReleaseDateDisplay").Visibility);
        });
    }

    private static TextBlock FindText(DependencyObject root, string text) =>
        Assert.Single(Descendants(root).OfType<TextBlock>(), element => element.Text == text);

    private static TextBlock FindByTextBinding(DependencyObject root, string path) =>
        Assert.Single(Descendants(root).OfType<TextBlock>(), element =>
            BindingOperations.GetBinding(element, TextBlock.TextProperty)?.Path?.Path == path);

    private static Border FindByVisibilityBinding(DependencyObject root, string path) =>
        Assert.Single(Descendants(root).OfType<Border>(), element =>
            BindingOperations.GetBinding(element, UIElement.VisibilityProperty)?.Path?.Path == path);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void FlushBindings(Dispatcher dispatcher) =>
        dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

    private static void RunSta(Action action)
        => PlaySteadWpfTestResources.Run(action);

    private sealed class FakeCatalogStore(CatalogContent? content) : ICanonicalCatalogStore
    {
        public Task<CatalogContent?> GetByIdAsync(CatalogContentId id, CancellationToken cancellationToken) => Task.FromResult(content);
        public Task<CatalogMetadata> GetMetadataAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogContent?> GetByPublicIdAsync(PlaySteadPublicId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogContent?> FindByProviderRefAsync(CatalogProviderKind provider, string externalId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogProviderRef>> GetProviderRefsAsync(CatalogContentId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogAlias>> GetAliasesAsync(CatalogContentId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogContentRelation>> GetRelationsFromAsync(CatalogContentId id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeProviderMetadataStore(params ProviderGameMetadata[] metadata) : IProviderGameMetadataStore
    {
        public Task<IReadOnlyList<ProviderGameMetadata>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderGameMetadata>>(metadata);

        public Task<ProviderGameMetadata?> GetAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult<ProviderGameMetadata?>(metadata.FirstOrDefault(value => value.GameId == gameId && value.Provider == provider));

        public Task UpsertAsync(ProviderGameMetadata metadata, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeManualMetadataLinkStore(ManualMetadataLink? link) : IManualMetadataLinkStore
    {
        public Task<ManualMetadataLink?> GetAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult(link?.ManualGameId == gameId ? link : null);
        public Task UpsertAsync(ManualMetadataLink link, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveAsync(GameId gameId, CancellationToken cancellationToken) => Task.CompletedTask;
        public ManualMetadataLink? TryGetCached(GameId gameId) => link?.ManualGameId == gameId ? link : null;
    }
}
