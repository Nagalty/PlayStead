using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.SingleInstance;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class SingleInstanceUiRegistrationTests
{
    [Fact]
    public void Build_registers_invocation_handler_and_window_activator()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        var layout = UserDataLayout.FromRoot(root);

        using var host = PlaySteadHost.Build(layout);

        var probe =
            host.Services.GetRequiredService<IServiceProviderIsService>();

        Assert.True(
            probe.IsService(typeof(IAppInvocationHandler)));

        Assert.True(
            probe.IsService(typeof(IWindowActivator)));
    }
}
