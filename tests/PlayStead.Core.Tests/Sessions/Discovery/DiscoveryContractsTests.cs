using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Tests.Sessions.Discovery;

public sealed class DiscoveryContractsTests
{
    private static readonly Guid Game = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Installation = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Generation = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Episode = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateTimeOffset Time = new(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(2));
    private const string Root = @"C:\Games\Example";
    private const string Path = @"C:\Games\Example\game.exe";

    [Fact]
    public void Revision_preserves_size_normalizes_utc_and_compares_by_value()
    {
        var revision = new FileRevision(12, Time);
        Assert.Equal(12, revision.SizeBytes);
        Assert.Equal(new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero), revision.LastWriteTimeUtc);
        Assert.Equal(revision, new FileRevision(12, Time.ToUniversalTime()));
    }

    [Fact]
    public void Revision_rejects_negative_size() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileRevision(-1, Time));

    [Fact]
    public void Scope_preserves_root_text_and_absence()
    {
        var scope = Scope(false, "  C:\\Games\\Example  ");
        Assert.False(scope.IsPresent);
        Assert.Equal("  C:\\Games\\Example  ", scope.RootPath);
        Assert.Equal(new GameId(Game), scope.GameId);
        Assert.Equal(new InstallationId(Installation), scope.InstallationId);
        Assert.Equal(Generation, scope.GenerationId);
    }

    [Fact]
    public void Scope_rejects_empty_identifiers_and_blank_root()
    {
        Assert.Throws<ArgumentException>(() => new InstallationScope(new GameId(Guid.Empty), new InstallationId(Installation), Root, Generation, true));
        Assert.Throws<ArgumentException>(() => new InstallationScope(new GameId(Game), new InstallationId(Guid.Empty), Root, Generation, true));
        Assert.Throws<ArgumentException>(() => new InstallationScope(new GameId(Game), new InstallationId(Installation), Root, Guid.Empty, true));
        Assert.Throws<ArgumentException>(() => Scope(root: " \t "));
    }

    [Fact]
    public void Candidate_requires_path_name_and_revision_without_filesystem_lookup()
    {
        var candidate = Candidate();
        Assert.Equal(Path, candidate.ExecutablePath);
        Assert.Equal("game.exe", candidate.ExecutableName);
        Assert.Throws<ArgumentException>(() => new ExecutableCandidate(" ", "game.exe", Revision()));
        Assert.Throws<ArgumentException>(() => new ExecutableCandidate(Path, " ", Revision()));
        Assert.Throws<ArgumentNullException>(() => new ExecutableCandidate(Path, "game.exe", null!));
    }

    [Fact]
    public void Inventory_rejects_invalid_completeness_and_issue_mismatch()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutableInventory(Scope(), (InventoryCompleteness)17, [], []));
        Assert.Throws<ArgumentException>(() => new ExecutableInventory(Scope(), InventoryCompleteness.Complete, [], [new InventoryIssue(Root, InventoryIssueKind.IoFailure)]));
        Assert.Throws<ArgumentException>(() => new ExecutableInventory(Scope(), InventoryCompleteness.Incomplete, [], []));
    }

    [Fact]
    public void Inventory_rejects_case_insensitive_duplicate_candidate_paths()
    {
        var other = new ExecutableCandidate(Path.ToUpperInvariant(), "GAME.EXE", Revision());
        Assert.Throws<ArgumentException>(() => new ExecutableInventory(Scope(), InventoryCompleteness.Complete, [Candidate(), other], []));
    }

    [Fact]
    public void Inventory_keeps_order_and_defensive_copies_of_candidates_and_issues()
    {
        var candidates = new List<ExecutableCandidate> { Candidate(), new(@"C:\Games\Example\second.exe", "second.exe", Revision()) };
        var issues = new List<InventoryIssue> { new(Root, InventoryIssueKind.IoFailure) };
        var inventory = new ExecutableInventory(Scope(), InventoryCompleteness.Incomplete, candidates, issues);
        candidates.Clear();
        issues.Clear();
        Assert.Equal(new[] { Path, @"C:\Games\Example\second.exe" }, inventory.Candidates.Select(x => x.ExecutablePath));
        Assert.Single(inventory.Issues);
        Assert.Throws<NotSupportedException>(() => ((IList<ExecutableCandidate>)inventory.Candidates).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<InventoryIssue>)inventory.Issues).Clear());
    }

    [Fact]
    public void Issue_rejects_blank_path_and_unknown_kind()
    {
        Assert.Throws<ArgumentException>(() => new InventoryIssue(" ", InventoryIssueKind.IoFailure));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryIssue(Root, (InventoryIssueKind)99));
    }

    [Fact]
    public void Range_has_inclusive_nonnegative_bounds()
    {
        Assert.Equal(new SnapshotRange(2, 2), new SnapshotRange(2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnapshotRange(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnapshotRange(2, 1));
    }

    [Fact]
    public void Evidence_allows_missing_revision_and_unreliable_identity()
    {
        var evidence = new CandidateEpisodeEvidence(Path, null, false, false, []);
        Assert.Null(evidence.Revision);
        Assert.False(evidence.HasReliablePath);
        Assert.False(evidence.HasReliableIdentity);
        Assert.Empty(evidence.PresenceRanges);
    }

    [Fact]
    public void Evidence_rejects_blank_path_null_elements_and_overlapping_or_adjacent_ranges()
    {
        Assert.Throws<ArgumentException>(() => new CandidateEpisodeEvidence(" ", null, false, false, []));
        Assert.Throws<ArgumentException>(() => Evidence([new SnapshotRange(4, 5), new SnapshotRange(2, 3)]));
        Assert.Throws<ArgumentException>(() => Evidence([new SnapshotRange(2, 5), new SnapshotRange(5, 7)]));
        Assert.Throws<ArgumentException>(() => Evidence([new SnapshotRange(2, 5), new SnapshotRange(6, 7)]));
        Assert.Throws<ArgumentException>(() => Evidence([new SnapshotRange(2, 5), null!]));
    }

    [Fact]
    public void Evidence_keeps_a_defensive_copy_of_ranges()
    {
        var ranges = new List<SnapshotRange> { new(2, 5) };
        var evidence = Evidence(ranges);
        ranges.Clear();
        Assert.Equal(new SnapshotRange(2, 5), Assert.Single(evidence.PresenceRanges));
        Assert.Throws<NotSupportedException>(() => ((IList<SnapshotRange>)evidence.PresenceRanges).Clear());
    }

    [Fact]
    public void Episode_normalizes_times_and_keeps_scope_sequence_and_quality()
    {
        var episode = MakeEpisode();
        Assert.Equal(Episode, episode.EpisodeId);
        Assert.Equal(1, episode.SequenceNumber);
        Assert.Equal(2, episode.PolicyVersion);
        Assert.Equal(Time.ToUniversalTime(), episode.StartedAtUtc);
        Assert.Equal(Time.AddMinutes(10).ToUniversalTime(), episode.EndedAtUtc);
        Assert.Equal(EpisodeQuality.Complete, episode.Quality);
        Assert.Equal(1, episode.FirstSnapshot);
        Assert.Equal(9, episode.LastSnapshot);
    }

    [Fact]
    public void Episode_rejects_empty_id_bad_sequence_version_or_snapshot_bounds()
    {
        Assert.Throws<ArgumentException>(() => MakeEpisode(id: Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => MakeEpisode(sequence: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MakeEpisode(version: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MakeEpisode(first: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MakeEpisode(first: 9, last: 8));
    }

    [Fact]
    public void Episode_rejects_reversed_times_and_unknown_quality_flags()
    {
        Assert.Throws<ArgumentException>(() => MakeEpisode(start: Time.AddMinutes(11)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MakeEpisode(quality: (EpisodeQuality)8));
        var valid = MakeEpisode(quality: EpisodeQuality.Partial | EpisodeQuality.CaptureGap | EpisodeQuality.UnknownProcessIdentity);
        Assert.Equal((EpisodeQuality)7, valid.Quality);
    }

    [Fact]
    public void Episode_rejects_ranges_outside_capture_bounds_and_duplicate_paths()
    {
        Assert.Throws<ArgumentException>(() => MakeEpisode(candidates: [Evidence([new SnapshotRange(0, 2)])]));
        Assert.Throws<ArgumentException>(() => MakeEpisode(candidates: [Evidence([new SnapshotRange(8, 10)])]));
        Assert.Throws<ArgumentException>(() => MakeEpisode(candidates: [Evidence([]), new CandidateEpisodeEvidence(Path.ToUpperInvariant(), null, false, false, [])]));
    }

    [Fact]
    public void Episode_keeps_defensive_copy_of_candidates()
    {
        var candidates = new List<CandidateEpisodeEvidence> { Evidence([new SnapshotRange(2, 5)]) };
        var episode = MakeEpisode(candidates: candidates);
        candidates.Clear();
        Assert.Single(episode.Candidates);
        Assert.Throws<NotSupportedException>(() => ((IList<CandidateEpisodeEvidence>)episode.Candidates).Clear());
    }

    [Fact]
    public void Decision_rejects_unknown_kind_and_invalid_main_shape()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryDecision((DiscoveryDecisionKind)99, null, [DiscoveryReason.NoCandidates]));
        Assert.Throws<ArgumentException>(() => new DiscoveryDecision(DiscoveryDecisionKind.PromoteMain, null, [DiscoveryReason.RepeatedQualifiedEpisodes]));
        Assert.Throws<ArgumentException>(() => new DiscoveryDecision(DiscoveryDecisionKind.Ambiguous, Candidate(), [DiscoveryReason.EquivalentCandidates]));
        Assert.Throws<ArgumentException>(() => new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence, Candidate(), [DiscoveryReason.NoCandidates]));
    }

    [Fact]
    public void Decision_requires_nonempty_distinct_valid_reasons_and_copies_them()
    {
        Assert.Throws<ArgumentException>(() => new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence, null, []));
        Assert.Throws<ArgumentException>(() => new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence, null, [DiscoveryReason.NoCandidates, DiscoveryReason.NoCandidates]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryDecision(DiscoveryDecisionKind.InsufficientEvidence, null, [(DiscoveryReason)99]));
        var reasons = new List<DiscoveryReason> { DiscoveryReason.RepeatedQualifiedEpisodes };
        var decision = new DiscoveryDecision(DiscoveryDecisionKind.PromoteMain, Candidate(), reasons);
        reasons.Clear();
        Assert.Equal(DiscoveryReason.RepeatedQualifiedEpisodes, Assert.Single(decision.Reasons));
        Assert.Throws<NotSupportedException>(() => ((IList<DiscoveryReason>)decision.Reasons).Clear());
    }

    [Fact]
    public void Evaluation_keeps_episode_order_and_duplicates_without_sorting()
    {
        var later = MakeEpisode(sequence: 2);
        var earlier = MakeEpisode(sequence: 1);
        var episodes = new List<LearningEpisodeSummary> { later, earlier, later };
        var evaluation = new DiscoveryEvaluation(Inventory(), episodes, true, ProcessSignatureOrigin.Discovered);
        episodes.Clear();
        Assert.Equal(new long[] { 2, 1, 2 }, evaluation.Episodes.Select(x => x.SequenceNumber));
        Assert.True(evaluation.HasAmbiguousInstallation);
        Assert.Equal(ProcessSignatureOrigin.Discovered, evaluation.ExistingSignatureOrigin);
        Assert.Throws<NotSupportedException>(() => ((IList<LearningEpisodeSummary>)evaluation.Episodes).Clear());
    }

    [Fact]
    public void Evaluation_rejects_nulls_and_unknown_signature_origin()
    {
        Assert.Throws<ArgumentNullException>(() => new DiscoveryEvaluation(null!, [], false, null));
        Assert.Throws<ArgumentNullException>(() => new DiscoveryEvaluation(Inventory(), null!, false, null));
        Assert.Throws<ArgumentException>(() => new DiscoveryEvaluation(Inventory(), [null!], false, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryEvaluation(Inventory(), [], false, (ProcessSignatureOrigin)99));
    }

    private static FileRevision Revision() => new(12, Time);
    private static InstallationScope Scope(bool present = true, string root = Root) => new(new GameId(Game), new InstallationId(Installation), root, Generation, present);
    private static ExecutableCandidate Candidate() => new(Path, "game.exe", Revision());
    private static ExecutableInventory Inventory() => new(Scope(), InventoryCompleteness.Complete, [Candidate()], []);
    private static CandidateEpisodeEvidence Evidence(IReadOnlyList<SnapshotRange> ranges) => new(Path, Revision(), true, true, ranges);
    private static LearningEpisodeSummary MakeEpisode(
        Guid? id = null, long sequence = 1, int version = 2, long first = 1, long last = 9,
        DateTimeOffset? start = null, EpisodeQuality quality = EpisodeQuality.Complete,
        IReadOnlyList<CandidateEpisodeEvidence>? candidates = null) =>
        new(id ?? Episode, sequence, Scope(), version, start ?? Time, Time.AddMinutes(10),
            first, last, quality, candidates ?? [Evidence([new SnapshotRange(2, 5)])]);
}
