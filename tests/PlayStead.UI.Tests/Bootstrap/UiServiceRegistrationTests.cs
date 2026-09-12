using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class UiServiceRegistrationTests
{
    [Fact]
    public void Build_registers_LibraryViewModel_and_MainWindow()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        var layout = UserDataLayout.FromRoot(root);

        using var host = PlaySteadHost.Build(layout);

        Assert.NotNull(
            host.Services.GetRequiredService<LibraryViewModel>());

        var registrationProbe =
            host.Services.GetRequiredService<IServiceProviderIsService>();

        Assert.True(
            registrationProbe.IsService(typeof(MainWindow)));
    }
}
