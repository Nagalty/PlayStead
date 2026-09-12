using System.Reflection;
using PlayStead.Platform.Paths;
using PlayStead.Providers.Steam;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class SteamRootLocatorRegistrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        nameof(SteamRootLocatorRegistrationTests),
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void PlaySteadHost_resolves_registry_backed_locator_without_constructor_override_candidates()
    {
        var layout = UserDataLayout.FromRoot(_root);
        layout.EnsureDirectoriesExist();

        using var host = PlaySteadHost.Build(layout);

        var locator = host.Services.GetService(
            typeof(WindowsSteamRootLocator));

        var typedLocator = Assert.IsType<WindowsSteamRootLocator>(
            locator);

        var overrideField = typeof(WindowsSteamRootLocator)
            .GetField(
                "_candidatesOverride",
                BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(overrideField);

        var overrideValue = overrideField.GetValue(
            typedLocator);

        Assert.Null(overrideValue);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }
}
