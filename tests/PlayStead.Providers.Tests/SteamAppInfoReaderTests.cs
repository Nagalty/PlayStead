using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests;

public sealed class SteamAppInfoReaderTests
{
    [Fact]
    public void Real_appinfo_resolves_enshrouded_exactly_when_present()
    {
        var path = @"C:\Program Files (x86)\Steam\appcache\appinfo.vdf";
        if (!File.Exists(path)) return;
        var result = new SteamAppInfoReader().Find(path, 1203620, "Enshrouded");
        Assert.NotNull(result);
        Assert.Equal("Keen Games GmbH", result!.Developer);
        Assert.Equal("Keen Games GmbH", result.Publisher);
    }

    [Fact]
    public void Real_appinfo_resolves_a_second_exact_appid_when_present()
    {
        var path = @"C:\Program Files (x86)\Steam\appcache\appinfo.vdf";
        if (!File.Exists(path)) return;
        var result = new SteamAppInfoReader().Find(path, 250820, "SteamVR");
        Assert.NotNull(result);
        Assert.Equal((uint)250820, result!.AppId);
    }
}
