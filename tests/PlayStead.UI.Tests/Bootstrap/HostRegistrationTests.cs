using Microsoft.Extensions.DependencyInjection;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.Providers.Steam;
using PlayStead.UI.Bootstrap;
using PlayStead.Platform.Paths;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class HostRegistrationTests
{
    [Fact]
    public void Build_registers_local_foundation_services_without_starting_WPF()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        var layout = UserDataLayout.FromRoot(root);

        using var host = PlaySteadHost.Build(layout);

        Assert.NotNull(
            host.Services.GetRequiredService<DatabaseInitializer>());

        Assert.NotNull(
            host.Services.GetRequiredService<DatabaseHealthChecker>());

        Assert.NotNull(
            host.Services.GetRequiredService<ILibraryStore>());

        Assert.NotNull(
            host.Services.GetRequiredService<LocalScanCoordinator>());

        var sources = host.Services
            .GetServices<ILocalLibrarySource>()
            .ToArray();

        Assert.Contains(
            sources,
            source => source is SteamLocalLibrarySource);
    }
}
