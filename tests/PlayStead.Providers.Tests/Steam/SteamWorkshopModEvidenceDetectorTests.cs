using PlayStead.Core.Library;
using PlayStead.Core.Modding;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamWorkshopModEvidenceDetectorTests
{
    [Fact]
    public async Task Workshop_content_directory_produces_possible_evidence_without_confirming_activation()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead-ModEvidence-" + Guid.NewGuid().ToString("N"));
        var install = Path.Combine(root, "steamapps", "common", "Game");
        Directory.CreateDirectory(Path.Combine(root, "steamapps", "workshop", "content", "123", "456"));
        try
        {
            var installation = new GameInstallation(InstallationId.New(), GameId.New(), ProviderKind.Steam, "123", install, null, true, true, DateTimeOffset.UtcNow);
            var result = await new SteamWorkshopModEvidenceDetector().DetectAsync(installation, CancellationToken.None);
            var evidence = Assert.Single(result);
            Assert.Equal(ModDetectionState.PossiblyModded, evidence.State);
            Assert.Equal(ModEvidenceKind.WorkshopContentPresent, evidence.EvidenceKind);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Missing_workshop_content_returns_no_evidence()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead-ModEvidence-" + Guid.NewGuid().ToString("N"));
        var install = Path.Combine(root, "steamapps", "common", "Game");
        Directory.CreateDirectory(install);
        try
        {
            var installation = new GameInstallation(InstallationId.New(), GameId.New(), ProviderKind.Steam, "123", install, null, true, true, DateTimeOffset.UtcNow);
            Assert.Empty(await new SteamWorkshopModEvidenceDetector().DetectAsync(installation, CancellationToken.None));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Invalid_install_path_is_unknown_without_throwing()
    {
        var installation = new GameInstallation(InstallationId.New(), GameId.New(), ProviderKind.Steam, "123", "\0invalid", null, true, true, DateTimeOffset.UtcNow);
        var result = await new SteamWorkshopModEvidenceDetector().DetectAsync(installation, CancellationToken.None);
        Assert.Empty(result);
    }
}
