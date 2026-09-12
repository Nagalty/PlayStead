using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class ApplicationRuntimeRegistrationTests
{
    [Fact]
    public void Build_registers_ApplicationRuntime()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        var layout = UserDataLayout.FromRoot(root);

        using var host = PlaySteadHost.Build(layout);

        var registrationProbe =
            host.Services.GetRequiredService<IServiceProviderIsService>();

        Assert.True(
            registrationProbe.IsService(typeof(ApplicationRuntime)));
    }
}
