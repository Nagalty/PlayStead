using PlayStead.UI.Navigation;
using PlayStead.UI.Shell;

namespace PlayStead.UI.Tests.Shell;

public sealed class ShellNavigationSearchTests
{
    [Fact]
    public void Search_command_publishes_non_empty_query()
    {
        var shell = new ShellViewModel(new NavigationService());
        string? received = null;
        shell.SearchRequested += (_, query) => received = query;

        shell.SetSearchQuery("arma");
        shell.SearchCommand.Execute(null);

        Assert.Equal("arma", received);
    }

    [Fact]
    public void Empty_search_is_a_noop()
    {
        var shell = new ShellViewModel(new NavigationService());
        var called = false;
        shell.SearchRequested += (_, _) => called = true;

        shell.SearchCommand.Execute(null);

        Assert.False(called);
    }
}
