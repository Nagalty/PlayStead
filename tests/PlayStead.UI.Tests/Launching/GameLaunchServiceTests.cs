using PlayStead.Core.Library;
using PlayStead.Core.ProviderInstallUpdate;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Tests.Launching;

public sealed class GameLaunchServiceTests
{
    [Fact]
    public void TryLaunch_opens_valid_Steam_uri_and_returns_true()
    {
        var opener =
            new RecordingUriLauncher();

        var service =
            new GameLaunchService(
                opener);

        var installation =
            CreateInstallation(
                ProviderKind.Steam,
                "1874880",
                isPresent: true);

        var launched =
            service.TryLaunch(
                installation);

        Assert.True(
            launched);

        Assert.Single(
            opener.OpenedUris);

        Assert.Equal(
            "steam://rungameid/1874880",
            opener.OpenedUris[0].AbsoluteUri);
    }

    [Fact]
    public void TryLaunch_does_not_open_uri_when_installation_is_unavailable()
    {
        var opener =
            new RecordingUriLauncher();

        var service =
            new GameLaunchService(
                opener);

        var installation =
            CreateInstallation(
                ProviderKind.Manual,
                "manual-id",
                isPresent: true);

        var launched =
            service.TryLaunch(
                installation);

        Assert.False(
            launched);

        Assert.Empty(
            opener.OpenedUris);
    }

    [Fact]
    public void CanLaunch_reports_only_supported_present_Steam_installations()
    {
        var service =
            new GameLaunchService(
                new RecordingUriLauncher());

        Assert.True(
            service.CanLaunch(
                CreateInstallation(
                    ProviderKind.Steam,
                    "1874880",
                    isPresent: true)));

        Assert.False(
            service.CanLaunch(
                CreateInstallation(
                    ProviderKind.Steam,
                    "1874880",
                    isPresent: false)));

        Assert.False(
            service.CanLaunch(
                CreateInstallation(
                    ProviderKind.Manual,
                    "manual-id",
                    isPresent: true)));
    }

    [Fact]
    public void Epic_valid_installation_is_launchable_and_opens_one_epic_uri()
    {
        var opener = new RecordingUriLauncher();
        var service = new GameLaunchService(opener);
        var installation = CreateEpicInstallation(isPresent: true);

        Assert.True(service.CanLaunch(installation));
        Assert.True(service.TryLaunch(installation));
        Assert.Single(opener.OpenedUris);
        Assert.StartsWith("com.epicgames.launcher://apps/", opener.OpenedUris[0].AbsoluteUri);
    }

    [Fact]
    public void Epic_incomplete_installation_is_not_launchable_and_does_not_fall_through_to_steam()
    {
        var opener = new RecordingUriLauncher();
        var service = new GameLaunchService(opener);
        var installation = CreateEpicInstallation(isPresent: true) with { LaunchMetadata = null };

        Assert.False(service.CanLaunch(installation));
        Assert.False(service.TryLaunch(installation));
        Assert.Empty(opener.OpenedUris);

        Assert.False(service.CanLaunch(CreateInstallation(ProviderKind.Gog, "gog-id", true)));
    }

    [Fact]
    public async Task Epic_paused_update_state_disables_launch()
    {
        var installation = CreateEpicInstallation(isPresent: true);
        var updates = new ProviderInstallUpdateStateReconciliationService(
            [new FakeUpdateSource(installation, ProviderInstallUpdateStatus.Downloading)]);
        await updates.RefreshAsync([installation], CancellationToken.None);
        Assert.Equal(ProviderInstallUpdateStatus.Downloading, updates.GetAll().Single().Status);

        var opener = new RecordingUriLauncher();
        var service = new GameLaunchService(opener, installUpdates: updates);

        Assert.False(service.CanLaunch(installation));
        Assert.False(service.TryLaunch(installation));
        Assert.Empty(opener.OpenedUris);
    }

    [Fact]
    public async Task Epic_current_state_keeps_launch_enabled()
    {
        var installation = CreateEpicInstallation(isPresent: true);
        var updates = new ProviderInstallUpdateStateReconciliationService(
            [new FakeUpdateSource(installation, ProviderInstallUpdateStatus.UpToDate)]);
        await updates.RefreshAsync([installation], CancellationToken.None);

        var service = new GameLaunchService(new RecordingUriLauncher(), installUpdates: updates);

        Assert.True(service.CanLaunch(installation));
    }

    private static GameInstallation CreateInstallation(
        ProviderKind provider,
        string externalId,
        bool isPresent)
    {
        return new GameInstallation(
            InstallationId.New(),
            GameId.New(),
            provider,
            externalId,
            @"D:\Games\Test",
            1_000_000_000,
            IsPreferred: true,
            IsPresent: isPresent,
            LastSeenUtc: new DateTimeOffset(
                2026,
                9,
                14,
                13,
                15,
                0,
                TimeSpan.Zero));
    }

    private static GameInstallation CreateEpicInstallation(bool isPresent) =>
        CreateInstallation(ProviderKind.Epic, "581c8d4fd9574884bff66cbdbaa42def", isPresent) with
        {
            LaunchMetadata = new ProviderLaunchMetadata(ProviderKind.Epic, new Dictionary<string, string>
            {
                ["CatalogNamespace"] = "6430e58041234e41b8f81f68f01450ed",
                ["CatalogItemId"] = "581c8d4fd9574884bff66cbdbaa42def",
                ["AppName"] = "3e02273b543f4ff0a1c24d3b534a9ac3"
            })
        };

    private sealed class RecordingUriLauncher :
        IExternalUriLauncher
    {
        public List<Uri> OpenedUris { get; } =
            [];

        public void Open(
            Uri uri)
        {
            OpenedUris.Add(
                uri);
        }
    }

    private sealed class FakeUpdateSource(
        GameInstallation installation,
        ProviderInstallUpdateStatus status) : IProviderInstallUpdateStateSource
    {
        public ProviderKind Provider => installation.Provider;

        public Task<IReadOnlyList<ProviderInstallUpdateState>> GetAsync(
            IReadOnlyCollection<GameInstallation> installations,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderInstallUpdateState>>(
                [new ProviderInstallUpdateState(
                    installation.GameId,
                    installation.Provider,
                    installation.ExternalId,
                    null,
                    null,
                    status,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    DateTimeOffset.UtcNow)]);
    }
}
