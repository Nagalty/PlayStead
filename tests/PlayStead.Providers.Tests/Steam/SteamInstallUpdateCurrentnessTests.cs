using PlayStead.Core.Library;
using PlayStead.Core.ProviderInstallUpdate;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamInstallUpdateCurrentnessTests
{
    private static readonly DateTimeOffset Observed =
        new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("appmanifest_1085660_completed.acf", "1085660", "24238629")]
    [InlineData("appmanifest_1172710_completed.acf", "1172710", "25486029")]
    [InlineData("appmanifest_1172620_completed.acf", "1172620", "25454928")]
    public void Captured_completed_manifest_is_current_despite_retained_counters(
        string fixtureName,
        string appId,
        string buildId)
    {
        var depots = appId switch
        {
            "1085660" => new Dictionary<string, string>
            {
                ["1085661"] = "4529650157997769787",
                ["1085662"] = "7375010213057600838",
                ["1085663"] = "1859000992539302057"
            },
            "1172710" => new Dictionary<string, string>
            {
                ["1172711"] = "8673686202082408760"
            },
            _ => new Dictionary<string, string>
            {
                ["1172621"] = "3846670688435861410",
                ["2838640"] = "502403447961499835"
            }
        };

        var evidence = new SteamAppManifestReader().ReadInstallUpdateEvidence(
            FixturePath(Path.Combine("InstallUpdate", fixtureName))) with
        {
            PublicBuildId = buildId,
            PublicDepotManifests = depots
        };

        var state = new ProviderInstallUpdateStateEvaluator().Evaluate(
            new GameId(Guid.NewGuid()),
            ProviderKind.Steam,
            appId,
            evidence,
            Observed);

        Assert.Equal(ProviderInstallUpdateStatus.UpToDate, state.Status);
    }

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Steam", name);
}
