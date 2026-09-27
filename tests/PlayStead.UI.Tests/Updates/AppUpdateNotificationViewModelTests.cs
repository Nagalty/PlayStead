using PlayStead.Core.Updates;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Updates;

namespace PlayStead.UI.Tests.Updates;

public sealed class AppUpdateNotificationViewModelTests
{
    [Fact]
    public void Unknown_state_is_hidden()
    {
        var service = new FakeUpdateService(AppUpdateState.Unknown(DistributionChannel.GitHub, "0.4.3"));
        using var viewModel = new AppUpdateNotificationViewModel(new AppUpdateCoordinator(service));

        Assert.False(viewModel.IsVisible);
        Assert.Empty(viewModel.ActionLabel);
    }

    [Fact]
    public async Task GitHub_update_projects_title_version_and_action_label()
    {
        var service = new FakeUpdateService(new AppUpdateState(
            AppUpdateStatus.UpdateAvailable,
            DistributionChannel.GitHub,
            "0.4.1",
            "0.4.3-alpha2",
            Action: AppUpdateActionKind.DownloadAndInstall));
        var coordinator = new AppUpdateCoordinator(service);
        using var viewModel = new AppUpdateNotificationViewModel(coordinator);

        coordinator.StartPostReadyCheck();
        await coordinator.CurrentCheck!;

        Assert.True(viewModel.IsVisible);
        Assert.Equal("Mise à jour PlayStead disponible", viewModel.Title);
        Assert.Equal("0.4.3-alpha2 est prête.", viewModel.Detail);
        Assert.Equal("Mettre à jour", viewModel.ActionLabel);
    }

    [Fact]
    public async Task Store_update_uses_store_action_label()
    {
        var service = new FakeUpdateService(new AppUpdateState(
            AppUpdateStatus.UpdateAvailable,
            DistributionChannel.MicrosoftStore,
            "0.4.1",
            Action: AppUpdateActionKind.OpenMicrosoftStore));
        var coordinator = new AppUpdateCoordinator(service);
        using var viewModel = new AppUpdateNotificationViewModel(coordinator);

        coordinator.StartPostReadyCheck();
        await coordinator.CurrentCheck!;

        Assert.True(viewModel.IsVisible);
        Assert.Equal("Ouvrir Microsoft Store", viewModel.ActionLabel);
    }

    private sealed class FakeUpdateService(AppUpdateState result) : IAppUpdateService
    {
        public DistributionChannel Channel => result.Channel;
        public AppUpdateState Current => result;
        public Task<AppUpdateState> CheckAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
