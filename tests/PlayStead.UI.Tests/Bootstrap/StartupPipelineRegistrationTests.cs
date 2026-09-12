using Microsoft.Extensions.DependencyInjection;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class StartupPipelineRegistrationTests
{
    [Fact]
    public void Build_registers_LocalStartupPipeline()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        var layout = UserDataLayout.FromRoot(root);

        using var host = PlaySteadHost.Build(layout);

        Assert.NotNull(
            host.Services.GetRequiredService<LocalStartupPipeline>());
    }
}
