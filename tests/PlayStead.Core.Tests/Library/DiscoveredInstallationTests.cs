using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Library;

public sealed class DiscoveredInstallationTests
{
    [Fact]
    public void Create_rejects_blank_external_id()
    {
        var ex = Assert.Throws<ArgumentException>(() => DiscoveredInstallation.Create(
            ProviderKind.Steam,
            " ",
            "Arma Reforger",
            @"G:\SteamLibrary\steamapps\common\Arma Reforger",
            42_000_000_000,
            DateTimeOffset.UtcNow));

        Assert.Contains("externalId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_rejects_blank_title()
    {
        var ex = Assert.Throws<ArgumentException>(() => DiscoveredInstallation.Create(
            ProviderKind.Steam,
            "1874880",
            " ",
            @"G:\SteamLibrary\steamapps\common\Arma Reforger",
            42_000_000_000,
            DateTimeOffset.UtcNow));

        Assert.Contains("title", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_rejects_blank_install_path()
    {
        var ex = Assert.Throws<ArgumentException>(() => DiscoveredInstallation.Create(
            ProviderKind.Steam,
            "1874880",
            "Arma Reforger",
            " ",
            42_000_000_000,
            DateTimeOffset.UtcNow));

        Assert.Contains("installPath", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_rejects_negative_installed_size()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DiscoveredInstallation.Create(
            ProviderKind.Steam,
            "1874880",
            "Arma Reforger",
            @"G:\SteamLibrary\steamapps\common\Arma Reforger",
            -1,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_normalizes_values_and_preserves_known_size()
    {
        var observed = new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

        var value = DiscoveredInstallation.Create(
            ProviderKind.Steam,
            " 1874880 ",
            " Arma Reforger ",
            @"G:\SteamLibrary\steamapps\common\Arma Reforger\.",
            42_000_000_000,
            observed);

        Assert.Equal(ProviderKind.Steam, value.Provider);
        Assert.Equal("1874880", value.ExternalId);
        Assert.Equal("Arma Reforger", value.Title);
        Assert.Equal(
            Path.GetFullPath(@"G:\SteamLibrary\steamapps\common\Arma Reforger"),
            value.InstallPath);
        Assert.Equal(42_000_000_000, value.InstalledSizeBytes);
        Assert.Equal(observed, value.ObservedAtUtc);
    }

    [Fact]
    public void Create_preserves_unknown_size()
    {
        var value = DiscoveredInstallation.Create(
            ProviderKind.Manual,
            "manual:test",
            "Test Game",
            @"C:\Games\Test Game",
            null,
            DateTimeOffset.UtcNow);

        Assert.Null(value.InstalledSizeBytes);
    }
}
