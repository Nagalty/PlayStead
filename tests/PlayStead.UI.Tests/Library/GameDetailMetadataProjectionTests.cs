using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

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

            // The general-info card is initially collapsed; loading metadata
            // updates the card binding and must also update each computed row flag.
            Assert.Equal(Visibility.Collapsed, FindByVisibilityBinding(view, "HasGeneralInfo").Visibility);
            vm.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            FlushBindings(view.Dispatcher);
            view.UpdateLayout();

            var card = FindByVisibilityBinding(view, "HasGeneralInfo");
            Assert.Equal(Visibility.Visible, card.Visibility);
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

            Assert.Equal(Visibility.Collapsed, FindText(view, "Date de sortie").Visibility);
            Assert.Equal(Visibility.Collapsed, FindByTextBinding(view, "ReleaseDateDisplay").Visibility);
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
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { error = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }

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
}
