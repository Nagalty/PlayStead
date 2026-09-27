using PlayStead.Core.Library;
using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.Core.Tests.ProviderInstallUpdate;

public sealed class ProviderInstallUpdateStateEvaluatorTests
{
    private static readonly DateTimeOffset Observed =
        new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly GameId Game =
        new(Guid.Parse("122ddfe7-4684-4586-b853-afcbe26ca486"));

    private readonly ProviderInstallUpdateStateEvaluator _sut = new();

    [Fact]
    public void Installed_build_different_from_target_without_pending_signal_is_version_mismatch()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "553850",
            Evidence(installed: "25327279", target: "25480438"), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.VersionMismatch, result.Status);
    }

    [Fact]
    public void Target_equal_to_installed_is_not_update_available()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "553850",
            Evidence(installed: "100", target: "100"), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public void Completed_download_and_staging_counters_are_ignored_when_currentness_is_confirmed()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "1085660",
            Evidence(
                installed: "24238629", target: "24238629",
                toDownload: 100, downloaded: 100,
                toStage: 200, staged: 200,
                publicBuild: "24238629",
                installedDepots: new Dictionary<string, string> { ["1085661"] = "dep-a" },
                publicDepots: new Dictionary<string, string> { ["1085661"] = "dep-a" }), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public void Incomplete_staging_is_staging_but_completed_staging_is_not()
    {
        var incomplete = _sut.Evaluate(Game, ProviderKind.Steam, "730",
            Evidence(installed: "100", target: "101", toStage: 100, staged: 40), Observed);
        var complete = _sut.Evaluate(Game, ProviderKind.Steam, "730",
            Evidence(installed: "100", target: "101", toStage: 100, staged: 100), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.Staging, incomplete.Status);
        Assert.Equal(ProviderInstallUpdateStatus.VersionMismatch, complete.Status);
    }

    [Fact]
    public void Completed_download_is_not_downloading()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "730",
            Evidence(installed: "100", target: "101", toDownload: 100, downloaded: 100), Observed);

        Assert.NotEqual(ProviderInstallUpdateStatus.Downloading, result.Status);
        Assert.Equal(ProviderInstallUpdateStatus.VersionMismatch, result.Status);
    }

    [Fact]
    public void Public_build_mismatch_without_pending_signal_is_version_mismatch()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "730",
            Evidence(installed: "100", target: "100", publicBuild: "101"), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.VersionMismatch, result.Status);
    }

    [Fact]
    public void Three_dmark_fixture_is_version_mismatch_without_pending_signal()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "223850",
            Evidence(installed: "14242816", target: "0", publicBuild: "25168177",
                installedDepots: new Dictionary<string, string> { ["223851"] = "776686087217928645" },
                publicDepots: new Dictionary<string, string> { ["223851"] = "4505947489952940915" }), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.VersionMismatch, result.Status);
    }

    [Fact]
    public void Helldivers_fixture_is_update_available()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "553850",
            Evidence(installed: "25327279", target: "25480438", toDownload: 86_653_644), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("25327279", result.InstalledBuildId);
        Assert.Equal("25480438", result.TargetBuildId);
    }

    [Fact]
    public void Download_progress_is_downloading()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "730",
            Evidence(installed: "100", target: "101", toDownload: 100, downloaded: 40), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.Downloading, result.Status);
    }

    [Fact]
    public void Staging_counters_are_staging()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "730",
            Evidence(installed: "100", target: "101", toStage: 1, staged: 0), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.Staging, result.Status);
    }

    [Fact]
    public void State_flags_alone_are_not_update_available()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "730",
            Evidence(stateFlags: 6), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.Unknown, result.Status);
    }

    [Fact]
    public void Missing_target_with_insufficient_evidence_is_unknown()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "730",
            Evidence(installed: "100"), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.Unknown, result.Status);
    }

    [Fact]
    public void Malformed_values_are_ignored_without_crashing()
    {
        var result = _sut.Evaluate(Game, ProviderKind.Steam, "730",
            new ProviderInstallUpdateEvidence("100", "101", "not-a-number", "oops", "bad", "also-bad", "still-bad", 6), Observed);

        Assert.Equal(ProviderInstallUpdateStatus.VersionMismatch, result.Status);
        Assert.Null(result.BytesToDownload);
    }

    private static ProviderInstallUpdateEvidence Evidence(
        string? installed = null,
        string? target = null,
        long? toDownload = null,
        long? downloaded = null,
        long? toStage = null,
        long? staged = null,
        long? stagingSize = null,
        int? stateFlags = null,
        string? publicBuild = null,
        IReadOnlyDictionary<string, string>? installedDepots = null,
        IReadOnlyDictionary<string, string>? publicDepots = null) =>
        new(installed, target, toDownload?.ToString(), downloaded?.ToString(), toStage?.ToString(), staged?.ToString(), stagingSize?.ToString(), stateFlags, publicBuild,
            installedDepots, publicDepots);
}
