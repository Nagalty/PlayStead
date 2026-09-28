using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamAppManifestReaderTests
{
    [Fact]
    public void Read_maps_known_fields_and_known_size()
    {
        var fixture = FixturePath("appmanifest_730.acf");
        var observed = new DateTimeOffset(
            2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

        var result = new SteamAppManifestReader().Read(
            fixture,
            @"G:\SteamLibrary",
            observed);

        Assert.Equal(ProviderKind.Steam, result.Provider);
        Assert.Equal("730", result.ExternalId);
        Assert.Equal("Counter-Strike 2", result.Title);
        Assert.Equal(
            Path.GetFullPath(
                @"G:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive"),
            result.InstallPath);
        Assert.Equal(42_000_000_000, result.InstalledSizeBytes);
        Assert.Equal(observed, result.ObservedAtUtc);
    }

    [Fact]
    public void Read_maps_known_fields_without_inventing_missing_size()
    {
        var fixture = FixturePath("appmanifest_missing_size.acf");
        var observed = new DateTimeOffset(
            2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

        var result = new SteamAppManifestReader().Read(
            fixture,
            @"G:\SteamLibrary",
            observed);

        Assert.Equal(ProviderKind.Steam, result.Provider);
        Assert.Equal("730", result.ExternalId);
        Assert.Null(result.InstalledSizeBytes);
        Assert.Equal(observed, result.ObservedAtUtc);
    }

    [Fact]
    public void Read_throws_when_AppState_is_missing()
    {
        var path = Path.GetTempFileName();

        try
        {
            File.WriteAllText(path, "\"NotAppState\" { \"appid\" \"730\" }");

            var ex = Assert.Throws<FormatException>(() =>
                new SteamAppManifestReader().Read(
                    path,
                    @"G:\SteamLibrary",
                    DateTimeOffset.UtcNow));

            Assert.Contains("AppState", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadInstallUpdateEvidence_maps_the_real_Helldivers_pending_update_fixture()
    {
        var fixture = FixturePath(Path.Combine("InstallUpdate", "appmanifest_553850_pending.acf"));

        var result = new SteamAppManifestReader().ReadInstallUpdateEvidence(fixture);

        Assert.Equal("25327279", result.InstalledBuildId);
        Assert.Equal("25480438", result.TargetBuildId);
        Assert.Equal("86653644", result.BytesToDownload);
        Assert.Equal("0", result.BytesDownloaded);
        Assert.Equal("0", result.BytesToStage);
        Assert.Equal("0", result.BytesStaged);
        Assert.Equal("0", result.StagingSize);
        Assert.Equal(6, result.StateFlags);
        Assert.Equal("4376562253430864477", result.InstalledDepotManifests!["553851"]);
    }

    [Fact]
    public void ReadInstallUpdateEvidence_maps_captured_completed_counters_and_depots()
    {
        var fixture = FixturePath(Path.Combine("InstallUpdate", "appmanifest_1172710_completed.acf"));

        var result = new SteamAppManifestReader().ReadInstallUpdateEvidence(fixture);

        Assert.Equal("25486029", result.InstalledBuildId);
        Assert.Equal("25486029", result.TargetBuildId);
        Assert.Equal("37375447382", result.BytesToStage);
        Assert.Equal("37375447382", result.BytesStaged);
        Assert.Equal("8673686202082408760", result.InstalledDepotManifests!["1172711"]);
    }

    [Fact]
    public void ReadActivity_maps_local_playtime_and_last_played_without_network()
    {
        var path = Path.GetTempFileName();
        var observed = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
        try
        {
            File.WriteAllText(path, """
                "AppState"
                {
                    "appid" "730"
                    "playtime_forever" "257280"
                    "LastPlayed" "1790596800"
                }
                """);

            var result = new SteamAppManifestReader().ReadActivity(
                path,
                new GameId(Guid.Parse("35e454ba-41f3-4df6-bbc6-1c15fc91fb63")),
                observed);

            Assert.Equal(TimeSpan.FromMinutes(257280), result.TotalPlaytime);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1790596800), result.LastPlayedAtUtc);
            Assert.Equal(ProviderKind.Steam, result.Source);
            Assert.Equal(observed, result.ObservedAtUtc);
            Assert.Equal(ProviderActivityAvailability.Complete, result.Availability);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string FixturePath(string name) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Steam",
            name);
}
