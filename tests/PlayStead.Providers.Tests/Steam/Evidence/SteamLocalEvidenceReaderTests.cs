using PlayStead.Providers.Steam.Evidence;

namespace PlayStead.Providers.Tests.Steam.Evidence;

public sealed class SteamLocalEvidenceReaderTests
{
    private static readonly DateTimeOffset ObservedAtUtc =
        new(2026, 9, 12, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Missing_beta_key_on_valid_manifest_means_public_branch()
    {
        var result = new SteamLocalEvidenceReader().Read(
            FixturePath("appmanifest_public.acf"),
            ObservedAtUtc);

        Assert.Equal("730", result.AppId);
        Assert.Equal("100", result.BuildId);
        Assert.Equal("public", result.BranchName);
        Assert.Equal(ObservedAtUtc, result.ObservedAtUtc);

        Assert.Equal(2, result.DepotManifestIds.Count);
        Assert.Equal("111", result.DepotManifestIds["731"]);
        Assert.Equal("222", result.DepotManifestIds["732"]);
    }

    [Fact]
    public void BetaKey_is_preserved_as_exact_branch_name()
    {
        var result = new SteamLocalEvidenceReader().Read(
            FixturePath("appmanifest_beta.acf"),
            ObservedAtUtc);

        Assert.Equal("440", result.AppId);
        Assert.Equal("200", result.BuildId);
        Assert.Equal("experimental", result.BranchName);
        Assert.Equal("333", result.DepotManifestIds["441"]);
    }

    [Fact]
    public void Empty_beta_key_is_normalized_to_public()
    {
        var result = new SteamLocalEvidenceReader().Read(
            FixturePath("appmanifest_empty_beta.acf"),
            ObservedAtUtc);

        Assert.Equal("public", result.BranchName);
    }

    [Fact]
    public void Missing_installed_depots_produces_empty_depot_evidence()
    {
        var result = new SteamLocalEvidenceReader().Read(
            FixturePath("appmanifest_missing_installed_depots.acf"),
            ObservedAtUtc);

        Assert.Equal("570", result.AppId);
        Assert.Equal("300", result.BuildId);
        Assert.Equal("public", result.BranchName);
        Assert.Empty(result.DepotManifestIds);
    }

    [Fact]
    public void Malformed_manifest_throws_format_exception()
    {
        Assert.Throws<FormatException>(() =>
            new SteamLocalEvidenceReader().Read(
                FixturePath("appmanifest_malformed.acf"),
                ObservedAtUtc));
    }

    private static string FixturePath(string name) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Steam",
            "Evidence",
            name);
}
