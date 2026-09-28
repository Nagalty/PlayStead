using System.Reflection;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Home;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Home;

public sealed class HomeViewModelTests :
    IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            nameof(HomeViewModelTests),
            Guid.NewGuid().ToString("N"));

    [Fact]
    public void Build_change_copy_uses_zero_hidden_singular_and_plural_forms()
    {
        Assert.Null(HomeViewModel.FormatBuildChangeText(0));
        Assert.Equal("1 mise à jour depuis ta dernière partie", HomeViewModel.FormatBuildChangeText(1));
        Assert.Equal("3 mises à jour depuis ta dernière partie", HomeViewModel.FormatBuildChangeText(3));
    }

    [Fact]
    public void Recently_played_card_metadata_uses_humanized_existing_display_formatters()
    {
        var now = DateTimeOffset.Now;
        var startedAt = now.AddDays(-1).AddHours(-1);
        var sessionLabel = startedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        var game = new HomeRecentlyPlayedGameViewModel(
            Guid.NewGuid(),
            "Example game",
            sessionLabel,
            "04:24",
            null,
            null);

        var dateProperty = typeof(HomeRecentlyPlayedGameViewModel).GetProperty("DisplayStartedAtLabel");
        var durationProperty = typeof(HomeRecentlyPlayedGameViewModel).GetProperty("DisplayDurationLabel");

        Assert.NotNull(dateProperty);
        Assert.NotNull(durationProperty);
        Assert.Equal(
            GameQuickPanelViewModel.FormatTimestampForDisplay(startedAt, now),
            dateProperty!.GetValue(game));
        Assert.Equal(
            GameQuickPanelViewModel.FormatDurationForDisplay(TimeSpan.FromSeconds(264)),
            durationProperty!.GetValue(game));
        Assert.Equal("4 min", durationProperty.GetValue(game));
    }

    [Fact]
    public void Empty_home_uses_existing_empty_projections_without_fake_values()
    {
        using var host =
            BuildHost();

        var library =
            host.Services
                .GetRequiredService<
                    LibraryViewModel>();

        var sessions =
            host.Services
                .GetRequiredService<
                    SessionViewModel>();

        var navigation =
            host.Services
                .GetRequiredService<
                    NavigationService>();

        var sut =
            new HomeViewModel(
                library,
                sessions,
                navigation);

        Assert.Equal(
            library.Items.Count,
            sut.LibraryGameCount);

        Assert.Empty(
            sut.ActiveSessions);

        Assert.Empty(
            sut.RecentSessions);

        Assert.Empty(
            sut.RecentlyPlayedGames);

        Assert.False(
            sut.HasRecentlyPlayedGames);

        Assert.False(
            sut.HasRecentActivity);

        Assert.Equal("Temps observé cette semaine", sut.WeeklyPlayTimeTitle);
        Assert.Equal("Sessions observées cette semaine", sut.WeeklySessionsTitle);
    }

    [Fact]
    public void Library_command_routes_to_Library_through_the_shared_navigation_service()
    {
        using var host =
            BuildHost();

        var navigation =
            host.Services
                .GetRequiredService<
                    NavigationService>();

        var sut =
            CreateViewModel(
                host,
                navigation);

        var command =
            FindCommand(
                sut,
                nameof(HomeViewModel.NavigateLibraryCommand));

        command.Execute(
            null);

        Assert.Equal(
            AppRoute.Library,
            navigation.CurrentRoute);
    }

    [Fact]
    public void Sessions_command_routes_to_contextual_Sessions_through_the_shared_navigation_service()
    {
        using var host =
            BuildHost();

        var navigation =
            host.Services
                .GetRequiredService<
                    NavigationService>();

        var sut =
            CreateViewModel(
                host,
                navigation);

        var command =
            FindCommand(
                sut,
                nameof(HomeViewModel.NavigateSessionsCommand));

        command.Execute(
            null);

        Assert.Equal(
            AppRoute.Sessions,
            navigation.CurrentRoute);
    }

    [Fact]
    public void Home_projection_reads_the_current_Library_and_Sessions_view_model_state()
    {
        using var host =
            BuildHost();

        var library =
            host.Services
                .GetRequiredService<
                    LibraryViewModel>();

        var sessions =
            host.Services
                .GetRequiredService<
                    SessionViewModel>();

        var navigation =
            host.Services
                .GetRequiredService<
                    NavigationService>();

        var sut =
            new HomeViewModel(
                library,
                sessions,
                navigation);

        Assert.Equal(
            library.Items.Count,
            sut.LibraryGameCount);

        Assert.Equal(
            sessions.ActiveSessions,
            sut.ActiveSessions);

        Assert.Equal(
            sessions.RecentSessions,
            sut.RecentSessions);

        Assert.Equal(
            sessions.HasRecentSessions,
            sut.HasRecentActivity);
    }

    private Microsoft.Extensions.Hosting.IHost
        BuildHost()
    {
        var layout =
            UserDataLayout.FromRoot(
                _root);

        layout.EnsureDirectoriesExist();

        return PlaySteadHost.Build(
            layout);
    }

    private static HomeViewModel CreateViewModel(
        Microsoft.Extensions.Hosting.IHost host,
        NavigationService navigation)
    {
        return new HomeViewModel(
            host.Services
                .GetRequiredService<
                    LibraryViewModel>(),
            host.Services
                .GetRequiredService<
                    SessionViewModel>(),
            navigation);
    }

    private static ICommand FindCommand(
        HomeViewModel viewModel,
        string propertyName)
    {
        var property =
            typeof(HomeViewModel)
                .GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public)
                .SingleOrDefault(
                    candidate =>
                        typeof(ICommand)
                            .IsAssignableFrom(
                                candidate.PropertyType) &&
                        candidate.Name.Equals(
                            propertyName,
                            StringComparison.Ordinal));

        Assert.NotNull(
            property);

        var command =
            Assert.IsAssignableFrom<
                ICommand>(
                property!.GetValue(
                    viewModel));

        Assert.True(
            command.CanExecute(
                null));

        return command;
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite
            .SqliteConnection
            .ClearAllPools();

        if (Directory.Exists(
                _root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }
}
