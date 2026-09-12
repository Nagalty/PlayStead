using PlayStead.Core.Library;
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

    private static string FixturePath(string name) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Steam",
            name);
}
