using PlayStead.Core.Library;
using PlayStead.Core.Modding;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamGameSpecificModEvidenceDetectorTests
{
    [Fact]
    public async Task Fallout4_vanilla_fixture_has_no_evidence()
    {
        using var fixture = new FalloutFixture();
        var result = await fixture.DetectAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task Fallout4_loader_without_plugin_is_only_possible()
    {
        using var fixture = new FalloutFixture();
        File.WriteAllText(Path.Combine(fixture.InstallPath, "f4se_loader.exe"), string.Empty);

        var result = await fixture.DetectAsync();
        var evidence = Assert.Single(result);
        Assert.Equal(ModEvidenceKind.ModLoaderPresent, evidence.EvidenceKind);
        Assert.Equal(ModDetectionState.PossiblyModded, evidence.State);
    }

    [Fact]
    public async Task Fallout4_loader_and_plugin_confirm_modding()
    {
        using var fixture = new FalloutFixture();
        File.WriteAllText(Path.Combine(fixture.InstallPath, "f4se_loader.exe"), string.Empty);
        Directory.CreateDirectory(Path.Combine(fixture.InstallPath, "Data", "F4SE", "Plugins"));
        File.WriteAllText(Path.Combine(fixture.InstallPath, "Data", "F4SE", "Plugins", "Example.dll"), string.Empty);

        var result = await fixture.DetectAsync();
        Assert.Contains(result, evidence => evidence.State == ModDetectionState.ConfirmedModded && evidence.EvidenceKind == ModEvidenceKind.ModFilesDetected);
    }

    [Fact]
    public async Task Removing_plugin_removes_confirmed_evidence()
    {
        using var fixture = new FalloutFixture();
        File.WriteAllText(Path.Combine(fixture.InstallPath, "f4se_loader.exe"), string.Empty);
        var plugins = Path.Combine(fixture.InstallPath, "Data", "F4SE", "Plugins");
        Directory.CreateDirectory(plugins);
        var plugin = Path.Combine(plugins, "Example.dll");
        File.WriteAllText(plugin, string.Empty);
        Assert.Contains(await fixture.DetectAsync(), evidence => evidence.State == ModDetectionState.ConfirmedModded);

        File.Delete(plugin);
        var refreshed = await fixture.DetectAsync();
        Assert.DoesNotContain(refreshed, evidence => evidence.State == ModDetectionState.ConfirmedModded);
    }

    [Fact]
    public async Task ReadyOrNot_reserved_mod_package_confirms_modding()
    {
        using var fixture = new ReadyOrNotFixture();
        var mods = Path.Combine(fixture.InstallPath, "ReadyOrNot", "Content", "Paks", "~mods");
        Directory.CreateDirectory(mods);
        File.WriteAllText(Path.Combine(mods, "ExampleMod.pak"), string.Empty);

        var result = await fixture.DetectAsync();

        var evidence = Assert.Single(result);
        Assert.Equal(ModEvidenceKind.ModFilesDetected, evidence.EvidenceKind);
        Assert.Equal(ModDetectionState.ConfirmedModded, evidence.State);
    }

    [Fact]
    public async Task ReadyOrNot_empty_reserved_mod_directory_has_no_evidence()
    {
        using var fixture = new ReadyOrNotFixture();
        Directory.CreateDirectory(Path.Combine(fixture.InstallPath, "ReadyOrNot", "Content", "Paks", "~mods"));

        Assert.Empty(await fixture.DetectAsync());
    }

    private sealed class FalloutFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-FalloutMod-" + Guid.NewGuid().ToString("N"));
        public string InstallPath => _root;

        public FalloutFixture() => Directory.CreateDirectory(_root);

        public Task<IReadOnlyList<ModEvidence>> DetectAsync() =>
            new SteamGameSpecificModEvidenceDetector().DetectAsync(
                new GameInstallation(InstallationId.New(), GameId.New(), ProviderKind.Steam, "377160", InstallPath, null, true, true, DateTimeOffset.UtcNow),
                CancellationToken.None);

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
    }

    private sealed class ReadyOrNotFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-ReadyOrNotMod-" + Guid.NewGuid().ToString("N"));
        public string InstallPath => _root;

        public ReadyOrNotFixture() => Directory.CreateDirectory(_root);

        public Task<IReadOnlyList<ModEvidence>> DetectAsync() =>
            new SteamGameSpecificModEvidenceDetector().DetectAsync(
                new GameInstallation(InstallationId.New(), GameId.New(), ProviderKind.Steam, "1144200", InstallPath, null, true, true, DateTimeOffset.UtcNow),
                CancellationToken.None);

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
    }
}
