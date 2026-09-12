using Microsoft.Extensions.DependencyInjection;
using PlayStead.Core.Steam;
using PlayStead.Data.Steam;
using PlayStead.Platform.Paths;
using PlayStead.Providers.Steam.Evidence;
using PlayStead.Providers.Steam.Remote;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Steam;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class SteamReferenceRuntimeRegistrationTests
{
    [Fact]
    public void Build_registers_complete_Steam_0_2_runtime_graph()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        var layout =
            UserDataLayout.FromRoot(root);

        using var host =
            PlaySteadHost.Build(layout);

        Assert.IsType<SqliteSteamEvidenceStore>(
            host.Services.GetRequiredService<ISteamEvidenceStore>());

        Assert.IsType<SteamLocalEvidenceSource>(
            host.Services.GetRequiredService<ISteamLocalEvidenceSource>());

        Assert.NotNull(
            host.Services.GetRequiredService<SteamUpdateStateEvaluator>());

        Assert.NotNull(
            host.Services.GetRequiredService<SteamCmdOptions>());

        Assert.NotNull(
            host.Services.GetRequiredService<SteamCmdPathResolver>());

        Assert.IsType<SystemSteamCmdProcessInvoker>(
            host.Services.GetRequiredService<ISteamCmdProcessInvoker>());

        Assert.IsType<SteamCmdRunner>(
            host.Services.GetRequiredService<ISteamCmdRunner>());

        Assert.IsType<SteamCmdAppInfoParser>(
            host.Services.GetRequiredService<ISteamCmdAppInfoParser>());

        Assert.IsType<SteamRemoteEvidenceProvider>(
            host.Services.GetRequiredService<ISteamRemoteEvidenceProvider>());

        Assert.NotNull(
            host.Services.GetRequiredService<
                SteamRemoteEvidenceFreshnessPolicy>());

        Assert.NotNull(
            host.Services.GetRequiredService<
                SteamRemoteRefreshCoordinator>());

        Assert.Same(
            TimeProvider.System,
            host.Services.GetRequiredService<TimeProvider>());

        Assert.IsType<SteamReferenceRuntime>(
            host.Services.GetRequiredService<ISteamReferenceRuntime>());
    }
}
