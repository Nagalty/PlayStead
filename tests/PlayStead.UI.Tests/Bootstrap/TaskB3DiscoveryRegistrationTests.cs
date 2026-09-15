using Microsoft.Extensions.DependencyInjection;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Data.Sessions;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class TaskB3DiscoveryRegistrationTests
{
    [Fact]
    public void Host_resolves_one_signature_authority_and_one_inventory_observer_validator_graph()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));
        using var host = PlaySteadHost.Build(UserDataLayout.FromRoot(root));
        var services = host.Services;

        var signatures = services.GetRequiredService<IProcessSignatureStore>();
        Assert.IsType<SqliteProcessSignatureStore>(signatures);
        Assert.Same(signatures, services.GetRequiredService<IProcessSignatureDiscoveryStore>());
        Assert.Same(signatures, services.GetRequiredService<IProcessSignatureStore>());

        var manager = services.GetRequiredService<DiscoveryInventoryManager>();
        Assert.Same(manager, services.GetRequiredService<DiscoveryInventoryManager>());
        var observer = services.GetRequiredService<IProcessCaptureObserver>();
        Assert.Same(observer, services.GetRequiredService<ProcessDiscoveryCaptureObserver>());
        Assert.Same(observer, services.GetRequiredService<IProcessCaptureObserver>());
        Assert.IsType<DiscoveredSignatureValidator>(services.GetRequiredService<IDiscoveredSignatureValidator>());
        Assert.Same(services.GetRequiredService<IDiscoveredSignatureValidator>(),
            services.GetRequiredService<IDiscoveredSignatureValidator>());
        Assert.NotNull(services.GetRequiredService<IExecutableInventorySource>());
        Assert.NotNull(services.GetRequiredService<IExecutableRevisionSource>());
        Assert.NotNull(services.GetRequiredService<IProcessSignatureLearningStore>());
    }

    [Fact]
    public void SessionRuntime_receives_registered_validator_and_capture_observer()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));
        using var host = PlaySteadHost.Build(UserDataLayout.FromRoot(root));
        var services = host.Services;
        var runtime = Assert.IsType<SessionRuntime>(services.GetRequiredService<ISessionRuntime>());

        var validator = typeof(SessionRuntime).GetField("_discoveredSignatureValidator",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var observer = typeof(SessionRuntime).GetField("_captureObserver",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.Same(services.GetRequiredService<IDiscoveredSignatureValidator>(), validator?.GetValue(runtime));
        Assert.Same(services.GetRequiredService<IProcessCaptureObserver>(), observer?.GetValue(runtime));
    }
}
