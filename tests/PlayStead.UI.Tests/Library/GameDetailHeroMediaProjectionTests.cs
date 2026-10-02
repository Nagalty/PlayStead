using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.UI.Library;
using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailHeroMediaProjectionTests
{
    [Fact]
    public void Game_detail_projects_cached_hero_path_and_exposes_has_hero()
    {
        var item = CreateItem();
        var viewModel = new GameDetailViewModel(item, null, null, @"C:\Media\steam\1203620\hero.jpg");

        Assert.Equal(@"C:\Media\steam\1203620\hero.jpg", viewModel.HeroPath);
        Assert.True(viewModel.HasHero);
    }

    [Fact]
    public void Missing_cached_hero_keeps_hero_absent()
    {
        var viewModel = new GameDetailViewModel(CreateItem(), null, null, null);

        Assert.Null(viewModel.HeroPath);
        Assert.False(viewModel.HasHero);
    }

    [Fact]
    public void Main_window_game_detail_factory_resolves_missing_hero_asynchronously()
    {
        var source = File.ReadAllText(FindUiFile("MainWindow.xaml.cs"));

        Assert.Contains("TryGetCachedPath", source, StringComparison.Ordinal);
        Assert.Contains("GameMediaAssetType.Hero", source, StringComparison.Ordinal);
        Assert.Contains("EnsureDetailHeroAsync", source, StringComparison.Ordinal);
        Assert.Contains("EnsureHeroAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_window_uses_local_provider_fallback_after_playstead_cache()
    {
        var source = File.ReadAllText(FindUiFile("MainWindow.xaml.cs"));

        Assert.Contains("ILocalGameMediaResolver", source, StringComparison.Ordinal);
        Assert.Contains("TryGetPath", source, StringComparison.Ordinal);
        Assert.Contains("_localGameMediaResolver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SteamLocalMediaLocator", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_projection_is_not_shared_between_game_details()
    {
        var first = new GameDetailViewModel(CreateItem(), null, null, "first-hero.jpg");
        var second = new GameDetailViewModel(CreateItem(), null, null, "second-hero.jpg");

        Assert.Equal("first-hero.jpg", first.HeroPath);
        Assert.Equal("second-hero.jpg", second.HeroPath);
    }

    [Fact]
    public void SetHeroPath_updates_hero_value_and_presence_notification()
    {
        var viewModel = new GameDetailViewModel(CreateItem(), null, null, null);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.SetHeroPath("resolved-hero.jpg");

        Assert.Equal("resolved-hero.jpg", viewModel.HeroPath);
        Assert.True(viewModel.HasHero);
        Assert.Contains(nameof(GameDetailViewModel.HeroPath), changed);
        Assert.Contains(nameof(GameDetailViewModel.HasHero), changed);
    }

    private static LibraryItemViewModel CreateItem() =>
        new(
            GameId.New(),
            "Game",
            ProviderKind.Steam,
            "Steam",
            string.Empty,
            null);

    private static string FindUiFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var uiDirectory = Path.Combine(directory.FullName, "src", "PlayStead.UI");
            if (Directory.Exists(uiDirectory))
            {
                var path = Path.Combine(uiDirectory, relativePath);
                Assert.True(File.Exists(path), $"Required UI file missing: {relativePath}");
                return path;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("PlayStead.UI source directory was not found.");
    }
}
