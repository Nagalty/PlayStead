using PlayStead.UI.Navigation;
using PlayStead.UI.Shell;

namespace PlayStead.UI.Tests.Navigation;

public sealed class ShellViewModelTests
{
    [Fact]
    public void New_shell_reflects_Home_as_the_active_primary_route()
    {
        var navigation = new NavigationService();
        var sut = new ShellViewModel(navigation);

        Assert.Equal(AppRoute.Home, sut.CurrentRoute);
        Assert.True(sut.IsHomeActive);
        Assert.False(sut.IsLibraryActive);
        Assert.False(sut.IsAttentionActive);
        Assert.False(sut.IsSettingsActive);
        Assert.False(sut.CanGoBack);
    }

    [Fact]
    public void Library_command_navigates_to_Library_and_updates_primary_active_state()
    {
        var navigation = new NavigationService();
        var sut = new ShellViewModel(navigation);

        sut.NavigateLibraryCommand.Execute(null);

        Assert.Equal(AppRoute.Library, sut.CurrentRoute);
        Assert.False(sut.IsHomeActive);
        Assert.True(sut.IsLibraryActive);
        Assert.False(sut.IsAttentionActive);
        Assert.False(sut.IsSettingsActive);
        Assert.True(sut.CanGoBack);
    }

    [Fact]
    public void Primary_commands_route_to_Home_Library_Attention_and_Settings()
    {
        var navigation = new NavigationService();
        var sut = new ShellViewModel(navigation);

        sut.NavigateAttentionCommand.Execute(null);
        Assert.Equal(AppRoute.Attention, sut.CurrentRoute);
        Assert.True(sut.IsAttentionActive);

        sut.NavigateSettingsCommand.Execute(null);
        Assert.Equal(AppRoute.Settings, sut.CurrentRoute);
        Assert.True(sut.IsSettingsActive);

        sut.NavigateHomeCommand.Execute(null);
        Assert.Equal(AppRoute.Home, sut.CurrentRoute);
        Assert.True(sut.IsHomeActive);

        sut.NavigateLibraryCommand.Execute(null);
        Assert.Equal(AppRoute.Library, sut.CurrentRoute);
        Assert.True(sut.IsLibraryActive);
    }

    [Fact]
    public void Contextual_Sessions_route_does_not_mark_a_primary_destination_active()
    {
        var navigation = new NavigationService();
        var sut = new ShellViewModel(navigation);

        navigation.Navigate(
            new NavigationRequest(
                AppRoute.Sessions));

        Assert.Equal(AppRoute.Sessions, sut.CurrentRoute);
        Assert.False(sut.IsHomeActive);
        Assert.False(sut.IsLibraryActive);
        Assert.False(sut.IsAttentionActive);
        Assert.False(sut.IsSettingsActive);
        Assert.True(sut.CanGoBack);
    }

    [Fact]
    public void GoBack_command_tracks_navigation_history_and_restores_previous_primary_route()
    {
        var navigation = new NavigationService();
        var sut = new ShellViewModel(navigation);

        sut.NavigateLibraryCommand.Execute(null);
        navigation.Navigate(
            new NavigationRequest(
                AppRoute.GameDetail,
                Guid.Parse("11111111-1111-1111-1111-111111111111")));

        Assert.Equal(AppRoute.GameDetail, sut.CurrentRoute);
        Assert.True(sut.CanGoBack);

        sut.GoBackCommand.Execute(null);

        Assert.Equal(AppRoute.Library, sut.CurrentRoute);
        Assert.True(sut.IsLibraryActive);
        Assert.True(sut.CanGoBack);

        sut.GoBackCommand.Execute(null);

        Assert.Equal(AppRoute.Home, sut.CurrentRoute);
        Assert.True(sut.IsHomeActive);
        Assert.False(sut.CanGoBack);
    }

    [Fact]
    public void Shell_notifies_route_active_state_and_back_state_when_navigation_changes()
    {
        var navigation = new NavigationService();
        var sut = new ShellViewModel(navigation);
        var changed = new List<string?>();

        sut.PropertyChanged +=
            (_, args) =>
                changed.Add(args.PropertyName);

        navigation.Navigate(
            new NavigationRequest(
                AppRoute.Library));

        Assert.Contains(nameof(ShellViewModel.CurrentRoute), changed);
        Assert.Contains(nameof(ShellViewModel.IsHomeActive), changed);
        Assert.Contains(nameof(ShellViewModel.IsLibraryActive), changed);
        Assert.Contains(nameof(ShellViewModel.IsAttentionActive), changed);
        Assert.Contains(nameof(ShellViewModel.IsSettingsActive), changed);
        Assert.Contains(nameof(ShellViewModel.CanGoBack), changed);
    }
}
