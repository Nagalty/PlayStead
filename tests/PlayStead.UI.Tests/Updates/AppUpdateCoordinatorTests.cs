using PlayStead.Core.Updates;
using PlayStead.Providers.Updates;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Updates;

public sealed class AppUpdateCoordinatorTests
{
    [Fact]
    public async Task GitHub_manifest_is_exposed_as_an_update_without_download()
    {
        var service = new GitHubAppUpdateService(
            "0.4.1-dev",
            _ => Task.FromResult<GitHubUpdateManifest?>(new GitHubUpdateManifest(
                "0.4.3",
                DistributionChannel.GitHub,
                new Uri("https://example.invalid/playstead.zip"),
                new string('a', 64))));

        var state = await service.CheckAsync();

        Assert.Equal(AppUpdateStatus.UpdateAvailable, state.Status);
        Assert.Equal(AppUpdateActionKind.DownloadAndInstall, state.Action);
    }

    [Fact]
    public async Task Coordinator_starts_only_one_post_ready_check()
    {
        var calls = 0;
        var service = new GitHubAppUpdateService(
            "0.4.1-dev",
            _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult<GitHubUpdateManifest?>(null);
            });
        var coordinator = new PlayStead.UI.Bootstrap.AppUpdateCoordinator(service);

        coordinator.StartPostReadyCheck();
        coordinator.StartPostReadyCheck();
        await coordinator.CurrentCheck!;

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Coordinator_exposes_last_state_after_post_ready_check()
    {
        var service = new StoreStateService(new AppUpdateState(
            AppUpdateStatus.ReadyToInstall,
            DistributionChannel.MicrosoftStore,
            "0.4.1",
            "0.4.2",
            Action: AppUpdateActionKind.OpenMicrosoftStore,
            LocalPackagePath: "C:\\invalid.zip"));
        var coordinator = new PlayStead.UI.Bootstrap.AppUpdateCoordinator(service);

        coordinator.StartPostReadyCheck();
        await coordinator.CurrentCheck!;

        Assert.Equal(AppUpdateStatus.ReadyToInstall, coordinator.LastState!.Status);
        Assert.Equal(DistributionChannel.MicrosoftStore, coordinator.LastState.Channel);
    }

    [Fact]
    public void Store_state_never_invokes_external_installer()
    {
        var installer = new FakeInstaller();
        var service = new GitHubAppUpdateService("0.4.1", _ => Task.FromResult<GitHubUpdateManifest?>(null));
        var coordinator = new PlayStead.UI.Bootstrap.AppUpdateCoordinator(service, installer: installer);
        Assert.False(coordinator.StartInstallation());
        Assert.False(installer.Started);
    }

    [Fact]
    public void Installation_is_started_only_once()
    {
        var installer = new FakeInstaller();
        var state = new AppUpdateState(AppUpdateStatus.ReadyToInstall, DistributionChannel.GitHub, "0.4.1", "0.4.3", LocalPackagePath: "C:\\package.zip");
        var coordinator = new AppUpdateCoordinator(new StoreStateService(state), installer: installer);
        Assert.True(coordinator.StartInstallation());
        Assert.False(coordinator.StartInstallation());
        Assert.Equal(1, installer.Calls);
    }

    private sealed class FakeInstaller : IAppUpdateInstaller
    {
        public bool Started { get; private set; }
        public int Calls { get; private set; }
        public bool TryStart(AppUpdateState state) { Started = true; Calls++; return true; }
    }

    private sealed class StoreStateService(AppUpdateState state) : IAppUpdateService
    {
        public DistributionChannel Channel => state.Channel;
        public AppUpdateState Current => state;
        public Task<AppUpdateState> CheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(state);
    }
}
