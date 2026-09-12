using PlayStead.Core.Steam;

namespace PlayStead.Core.Tests.Steam;

public sealed class SteamUpdateStateEvaluatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 18, 45, 0, TimeSpan.Zero);

    private readonly SteamUpdateStateEvaluator _sut = new();

    [Fact]
    public void Matching_branch_and_depots_is_up_to_date()
    {
        var local = Local(
            branch: "public",
            build: "100",
            ("228981", "111"),
            ("228982", "222"));

        var remote = Success(
            branch: "public",
            build: "100",
            ("228981", "111"),
            ("228982", "222"));

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.UpToDate, result.State);
        Assert.Equal(SteamUpdateReason.DepotManifestsMatch, result.Reason);
        Assert.Empty(result.ChangedDepotIds);
    }

    [Fact]
    public void One_changed_depot_is_update_available()
    {
        var local = Local(
            branch: "public",
            build: "100",
            ("228981", "111"));

        var remote = Success(
            branch: "public",
            build: "101",
            ("228981", "999"));

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.UpdateAvailable, result.State);
        Assert.Equal(SteamUpdateReason.DepotManifestMismatch, result.Reason);
        Assert.Equal(["228981"], result.ChangedDepotIds);
    }

    [Fact]
    public void Multiple_changed_depots_are_returned_in_stable_order()
    {
        var local = Local(
            branch: "public",
            build: "100",
            ("300", "aaa"),
            ("100", "bbb"),
            ("200", "ccc"));

        var remote = Success(
            branch: "public",
            build: "101",
            ("300", "xxx"),
            ("100", "yyy"),
            ("200", "ccc"));

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.UpdateAvailable, result.State);
        Assert.Equal(SteamUpdateReason.DepotManifestMismatch, result.Reason);
        Assert.Equal(["100", "300"], result.ChangedDepotIds);
    }

    [Fact]
    public void Changed_build_with_matching_depots_is_new_version_detected()
    {
        var local = Local(
            branch: "public",
            build: "100",
            ("228981", "111"));

        var remote = Success(
            branch: "public",
            build: "101",
            ("228981", "111"));

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.NewVersionDetected, result.State);
        Assert.Equal(
            SteamUpdateReason.RemoteBuildChangedWithoutDepotDifference,
            result.Reason);
        Assert.Empty(result.ChangedDepotIds);
    }

    [Fact]
    public void Changed_build_with_no_remote_depots_is_new_version_detected()
    {
        var local = Local(
            branch: "public",
            build: "100",
            ("228981", "111"));

        var remote = Success(
            branch: "public",
            build: "101");

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.NewVersionDetected, result.State);
        Assert.Equal(
            SteamUpdateReason.RemoteBuildChangedWithIncompleteDepotEvidence,
            result.Reason);
        Assert.Empty(result.ChangedDepotIds);
    }

    [Fact]
    public void Unknown_local_branch_never_falls_back_to_public()
    {
        var local = Local(
            branch: null,
            build: "100",
            ("228981", "111"));

        var remote = Success(
            branch: "public",
            build: "101",
            ("228981", "999"));

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.Unknown, result.State);
        Assert.Equal(SteamUpdateReason.LocalBranchUnknown, result.Reason);
        Assert.Empty(result.ChangedDepotIds);
    }

    [Fact]
    public void Remote_branch_unavailable_is_unknown()
    {
        var local = Local(
            branch: "experimental",
            build: "100",
            ("228981", "111"));

        var remote = new SteamRemoteEvidenceResult(
            SteamRemoteEvidenceStatus.BranchUnavailable,
            Evidence: null,
            SteamRemoteFailureKind.BranchUnavailable);

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.Unknown, result.State);
        Assert.Equal(SteamUpdateReason.RemoteBranchUnavailable, result.Reason);
        Assert.Empty(result.ChangedDepotIds);
    }

    [Fact]
    public void Empty_local_depot_evidence_is_unknown()
    {
        var local = Local(
            branch: "public",
            build: "100");

        var remote = Success(
            branch: "public",
            build: "100",
            ("228981", "111"));

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.Unknown, result.State);
        Assert.Equal(SteamUpdateReason.LocalDepotEvidenceMissing, result.Reason);
        Assert.Empty(result.ChangedDepotIds);
    }

    [Fact]
    public void Empty_remote_depot_evidence_without_build_change_is_unknown()
    {
        var local = Local(
            branch: "public",
            build: "100",
            ("228981", "111"));

        var remote = Success(
            branch: "public",
            build: "100");

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.Unknown, result.State);
        Assert.Equal(SteamUpdateReason.RemoteDepotEvidenceMissing, result.Reason);
        Assert.Empty(result.ChangedDepotIds);
    }

    [Fact]
    public void Partial_remote_depot_evidence_is_unknown()
    {
        var local = Local(
            branch: "public",
            build: "100",
            ("228981", "111"),
            ("228982", "222"));

        var remote = Success(
            branch: "public",
            build: "101",
            ("228981", "999"));

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.Unknown, result.State);
        Assert.Equal(SteamUpdateReason.RemoteDepotEvidenceMissing, result.Reason);
        Assert.Empty(result.ChangedDepotIds);
    }

    [Fact]
    public void Branch_mismatch_is_contradictory_evidence()
    {
        var local = Local(
            branch: "experimental",
            build: "100",
            ("228981", "111"));

        var remote = Success(
            branch: "public",
            build: "101",
            ("228981", "999"));

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.Unknown, result.State);
        Assert.Equal(SteamUpdateReason.EvidenceContradictory, result.Reason);
        Assert.Empty(result.ChangedDepotIds);
    }

    [Fact]
    public void Different_app_ids_are_contradictory_evidence()
    {
        var local = Local(
            branch: "public",
            build: "100",
            ("228981", "111"));

        var remote = new SteamRemoteEvidenceResult(
            SteamRemoteEvidenceStatus.Success,
            new SteamRemoteEvidence(
                "999",
                "public",
                "101",
                DepotMap(("228981", "999")),
                Now,
                SteamRemoteEvidenceSource.SteamCmdAnonymous),
            FailureKind: null);

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.Unknown, result.State);
        Assert.Equal(SteamUpdateReason.EvidenceContradictory, result.Reason);
        Assert.Empty(result.ChangedDepotIds);
    }

    [Fact]
    public void Refresh_failure_without_remote_evidence_is_unknown()
    {
        var local = Local(
            branch: "public",
            build: "100",
            ("228981", "111"));

        var remote = new SteamRemoteEvidenceResult(
            SteamRemoteEvidenceStatus.RefreshFailed,
            Evidence: null,
            SteamRemoteFailureKind.Timeout);

        var result = _sut.Evaluate(local, remote, Now);

        Assert.Equal(SteamUpdateState.Unknown, result.State);
        Assert.Equal(
            SteamUpdateReason.RemoteRefreshFailedWithoutCache,
            result.Reason);
        Assert.Empty(result.ChangedDepotIds);
    }

    [Fact]
    public void Evaluator_never_returns_checking()
    {
        var local = Local(
            branch: "public",
            build: "100",
            ("228981", "111"));

        var remote = Success(
            branch: "public",
            build: "100",
            ("228981", "111"));

        var result = _sut.Evaluate(local, remote, Now);

        Assert.NotEqual(SteamUpdateState.Checking, result.State);
    }

    private static SteamLocalEvidence Local(
        string? branch,
        string? build,
        params (string DepotId, string ManifestId)[] depots)
        => new(
            "730",
            build,
            branch,
            DepotMap(depots),
            Now);

    private static SteamRemoteEvidenceResult Success(
        string branch,
        string? build,
        params (string DepotId, string ManifestId)[] depots)
        => new(
            SteamRemoteEvidenceStatus.Success,
            new SteamRemoteEvidence(
                "730",
                branch,
                build,
                DepotMap(depots),
                Now,
                SteamRemoteEvidenceSource.SteamCmdAnonymous),
            FailureKind: null);

    private static IReadOnlyDictionary<string, string> DepotMap(
        params (string DepotId, string ManifestId)[] depots)
        => depots.ToDictionary(
            x => x.DepotId,
            x => x.ManifestId,
            StringComparer.Ordinal);
}
