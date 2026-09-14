using PlayStead.Core.Library;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Tests.Launching;

public sealed class SteamLaunchUriFactoryTests
{
    [Fact]
    public void CreateOrNull_returns_rungameid_uri_for_present_Steam_installation()
    {
        var installation =
            CreateInstallation(
                ProviderKind.Steam,
                "1874880",
                isPresent: true);

        var uri =
            SteamLaunchUriFactory.CreateOrNull(
                installation);

        Assert.NotNull(
            uri);

        Assert.Equal(
            "steam://rungameid/1874880",
            uri.AbsoluteUri);
    }

    [Fact]
    public void CreateOrNull_returns_null_for_non_Steam_installation()
    {
        var installation =
            CreateInstallation(
                ProviderKind.Manual,
                "manual-id",
                isPresent: true);

        var uri =
            SteamLaunchUriFactory.CreateOrNull(
                installation);

        Assert.Null(
            uri);
    }

    [Fact]
    public void CreateOrNull_returns_null_when_installation_is_absent()
    {
        var installation =
            CreateInstallation(
                ProviderKind.Steam,
                "1874880",
                isPresent: false);

        var uri =
            SteamLaunchUriFactory.CreateOrNull(
                installation);

        Assert.Null(
            uri);
    }

    [Fact]
    public void CreateOrNull_returns_null_when_Steam_external_id_is_blank()
    {
        var installation =
            CreateInstallation(
                ProviderKind.Steam,
                "   ",
                isPresent: true);

        var uri =
            SteamLaunchUriFactory.CreateOrNull(
                installation);

        Assert.Null(
            uri);
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
                0,
                0,
                TimeSpan.Zero));
    }
}
