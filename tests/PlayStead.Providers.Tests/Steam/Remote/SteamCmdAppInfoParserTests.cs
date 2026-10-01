using PlayStead.Core.Steam;
using PlayStead.Providers.Steam.Remote;

namespace PlayStead.Providers.Tests.Steam.Remote;

public sealed class SteamCmdAppInfoParserTests
{
    private static readonly DateTimeOffset ObservedAtUtc =
        new(2026, 9, 12, 18, 45, 0, TimeSpan.Zero);

    private readonly SteamCmdAppInfoParser _sut = new();

    [Fact]
    public void Parses_public_branch_build_and_depot_manifests()
    {
        var result = _sut.Parse(
            "730",
            "public",
            FixtureText("appinfo_public.txt"),
            ObservedAtUtc);

        Assert.Equal(
            SteamRemoteEvidenceStatus.Success,
            result.Status);

        Assert.Null(result.FailureKind);
        Assert.NotNull(result.Evidence);

        var evidence = result.Evidence!;

        Assert.Equal("730", evidence.AppId);
        Assert.Equal("public", evidence.BranchName);
        Assert.Equal("101", evidence.BuildId);
        Assert.Equal(ObservedAtUtc, evidence.ObservedAtUtc);
        Assert.Equal(
            SteamRemoteEvidenceSource.SteamCmdAnonymous,
            evidence.Source);

        Assert.Equal(2, evidence.DepotManifestIds.Count);
        Assert.Equal("111", evidence.DepotManifestIds["731"]);
        Assert.Equal("222", evidence.DepotManifestIds["732"]);
    }

    [Fact]
    public void Parses_named_beta_branch_exactly()
    {
        var result = _sut.Parse(
            "440",
            "experimental",
            FixtureText("appinfo_experimental.txt"),
            ObservedAtUtc);

        Assert.Equal(
            SteamRemoteEvidenceStatus.Success,
            result.Status);

        var evidence = Assert.IsType<SteamRemoteEvidence>(
            result.Evidence);

        Assert.Equal("experimental", evidence.BranchName);
        Assert.Equal("201", evidence.BuildId);
        Assert.Equal("333", evidence.DepotManifestIds["441"]);
    }

    [Fact]
    public void Missing_requested_branch_is_branch_unavailable()
    {
        var result = _sut.Parse(
            "730",
            "experimental",
            FixtureText("appinfo_branch_absent.txt"),
            ObservedAtUtc);

        Assert.Equal(
            SteamRemoteEvidenceStatus.BranchUnavailable,
            result.Status);

        Assert.Null(result.Evidence);
        Assert.Equal(
            SteamRemoteFailureKind.BranchUnavailable,
            result.FailureKind);
    }

    [Fact]
    public void Malformed_output_is_refresh_failed()
    {
        var result = _sut.Parse(
            "730",
            "public",
            FixtureText("appinfo_malformed.txt"),
            ObservedAtUtc);

        Assert.Equal(
            SteamRemoteEvidenceStatus.RefreshFailed,
            result.Status);

        Assert.Null(result.Evidence);
        Assert.Equal(
            SteamRemoteFailureKind.MalformedOutput,
            result.FailureKind);
    }

    [Fact]
    public void Valid_branch_without_depot_manifests_is_success_with_empty_depot_set()
    {
        var result = _sut.Parse(
            "730",
            "public",
            FixtureText("appinfo_no_depot_manifests.txt"),
            ObservedAtUtc);

        Assert.Equal(
            SteamRemoteEvidenceStatus.Success,
            result.Status);

        var evidence = Assert.IsType<SteamRemoteEvidence>(
            result.Evidence);

        Assert.Equal("102", evidence.BuildId);
        Assert.Empty(evidence.DepotManifestIds);
    }

    [Fact]
    public void App_id_mismatch_in_payload_is_malformed_output()
    {
        var result = _sut.Parse(
            "730",
            "public",
            FixtureText("appinfo_other_app.txt"),
            ObservedAtUtc);

        Assert.Equal(
            SteamRemoteEvidenceStatus.RefreshFailed,
            result.Status);

        Assert.Null(result.Evidence);
        Assert.Equal(
            SteamRemoteFailureKind.MalformedOutput,
            result.FailureKind);
    }

    [Fact]
    public void Parses_modern_library_asset_hash_and_filenames()
    {
        var metadata = _sut.ParseMediaAssets(
            "3768760",
            FixtureText("appinfo_media_007.txt"));

        Assert.NotNull(metadata);
        Assert.Equal("1159a696d257cbeb3f4479be3466cfba2ae938a0", metadata!.LibraryAssetHash);
        Assert.Equal(2, metadata.CoverAssets?.Count);
        Assert.Equal("library_600x900_2x.jpg", metadata.CoverAssets![0].FileName);
        Assert.Contains(
            metadata.CoverAssets!,
            asset => asset.FileName == "library_600x900.jpg");
        Assert.Contains(
            metadata.CoverAssets!,
            asset => asset.FileName == "library_600x900_2x.jpg");
    }

    private static string FixtureText(string name) =>
        File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "Steam",
                "Remote",
                name));
}
