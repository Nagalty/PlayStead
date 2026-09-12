using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlayStead.Core.Sessions;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class SessionRuntimeRegistrationTests
{
    [Fact]
    public void Host_registers_session_runtime_dependencies_and_monitor_as_hosted_service()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        using var host =
            PlaySteadHost.Build(
                UserDataLayout.FromRoot(root));

        Assert.NotNull(
            host.Services.GetRequiredService<IProcessSnapshotSource>());

        Assert.NotNull(
            host.Services.GetRequiredService<IProcessSignatureStore>());

        Assert.NotNull(
            host.Services.GetRequiredService<ISessionStore>());

        Assert.NotNull(
            host.Services.GetRequiredService<ISessionRuntime>());

        var options =
            host.Services.GetRequiredService<SessionMonitorOptions>();

        Assert.Equal(
            TimeSpan.FromSeconds(2),
            options.PollInterval);

        var monitor =
            host.Services.GetRequiredService<SessionMonitor>();

        Assert.Contains(
            host.Services.GetServices<IHostedService>(),
            service => ReferenceEquals(
                service,
                monitor));
    }
}
