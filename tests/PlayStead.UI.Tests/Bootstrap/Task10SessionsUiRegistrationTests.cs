using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class Task10SessionsUiRegistrationTests
{
    [Fact]
    public void SessionViewModel_exposes_active_sessions_and_is_registered_by_the_host()
    {
        var viewModelType =
            typeof(MainWindow)
                .Assembly
                .GetType(
                    "PlayStead.UI.Sessions.SessionViewModel");

        Assert.True(
            viewModelType is not null,
            "Task 10 requires PlayStead.UI.Sessions.SessionViewModel.");

        Assert.NotNull(
            viewModelType!.GetProperty(
                "ActiveSessions"));

        Assert.NotNull(
            viewModelType.GetProperty(
                "HasActiveSessions"));

        var root =
            Path.Combine(
                Path.GetTempPath(),
                "PlayStead.Tests",
                nameof(Task10SessionsUiRegistrationTests),
                Guid.NewGuid().ToString("N"));

        var layout =
            UserDataLayout.FromRoot(
                root);

        using var host =
            PlaySteadHost.Build(
                layout);

        var registrationProbe =
            host.Services
                .GetRequiredService<
                    IServiceProviderIsService>();

        Assert.True(
            registrationProbe.IsService(
                viewModelType),
            "SessionViewModel must be available from the Generic Host.");
    }

    [Fact]
    public void SessionsViewModel_contract_keeps_task10_focused_on_active_sessions()
    {
        var viewModelType =
            typeof(MainWindow)
                .Assembly
                .GetType(
                    "PlayStead.UI.Sessions.SessionViewModel");

        Assert.True(
            viewModelType is not null,
            "Task 10 requires PlayStead.UI.Sessions.SessionViewModel.");

        var activeSessions =
            viewModelType!.GetProperty(
                "ActiveSessions");

        var hasActiveSessions =
            viewModelType.GetProperty(
                "HasActiveSessions");

        Assert.NotNull(
            activeSessions);

        Assert.NotNull(
            hasActiveSessions);

        Assert.Equal(
            typeof(bool),
            hasActiveSessions!.PropertyType);

        Assert.True(
            typeof(System.Collections.IEnumerable)
                .IsAssignableFrom(
                    activeSessions!.PropertyType),
            "ActiveSessions must be enumerable for WPF binding.");
    }
}
