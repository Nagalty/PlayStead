using PlayStead.Core.Library;
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
}
