using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Tests.Sessions.Discovery;

public sealed class ProcessSignatureDiscoveryPolicyTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;
    private static readonly FileRevision Revision = new(10, T0);
    private static readonly InstallationScope Scope = new(
        new GameId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        new InstallationId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
        @"C:\Games\Example", Guid.Parse("33333333-3333-3333-3333-333333333333"), true);
    private static readonly ExecutableCandidate Game = new(@"C:\Games\Example\game.exe", "game.exe", Revision);
    private static readonly ExecutableCandidate Companion = new(@"C:\Games\Example\companion.exe", "companion.exe", Revision);
    private static readonly ExecutableCandidate Third = new(@"C:\Games\Example\third.exe", "third.exe", Revision);

    [Fact]
    public void Same_candidate_two_complete_episodes_promotes_exact_main()
    {
        var result = Evaluate([Episode(1, Evidence(Game, new SnapshotRange(2, 7))), Episode(2, Evidence(Game, new SnapshotRange(2, 7)))], [Game]);
        AssertDecision(result, DiscoveryDecisionKind.PromoteMain, DiscoveryReason.RepeatedQualifiedEpisodes, Game);
    }

    [Theory]
    [InlineData(2, 4)]
    [InlineData(3, 4)]
    public void Startup_companion_before_or_with_main_can_qualify_twice(long start, long end)
    {
        var episodes = new[]
        {
            Episode(1, Evidence(Game, new SnapshotRange(3, 7)), Evidence(Companion, new SnapshotRange(start, end))),
            Episode(2, Evidence(Game, new SnapshotRange(3, 7)), Evidence(Companion, new SnapshotRange(start, end)))
        };
        AssertDecision(Evaluate(episodes, [Game, Companion]), DiscoveryDecisionKind.PromoteMain,
            DiscoveryReason.RepeatedQualifiedEpisodes, Game);
    }

    [Fact]
    public void Separate_main_ranges_and_one_global_absence_can_qualify()
    {
        var episode = Episode(1, Evidence(Game, new SnapshotRange(3, 4), new SnapshotRange(6, 7)), Evidence(Companion, new SnapshotRange(2, 4)));
        AssertDecision(Evaluate([episode, Episode(2, Evidence(Game, new SnapshotRange(3, 4), new SnapshotRange(6, 7)),
            Evidence(Companion, new SnapshotRange(2, 4)))], [Game, Companion]),
            DiscoveryDecisionKind.PromoteMain, DiscoveryReason.RepeatedQualifiedEpisodes, Game);
    }

    [Fact]
    public void Path_case_only_difference_is_the_same_candidate()
    {
        var lower = new ExecutableCandidate(Game.ExecutablePath.ToLowerInvariant(), "GAME.EXE", Revision);
        AssertDecision(Evaluate([Episode(1, Evidence(Game, new SnapshotRange(2, 7))),
            Episode(2, Evidence(lower, new SnapshotRange(2, 7)))], [Game]),
            DiscoveryDecisionKind.PromoteMain, DiscoveryReason.RepeatedQualifiedEpisodes, Game);
    }

    [Fact]
    public void One_episode_awaits_an_independent_episode() =>
        AssertDecision(Evaluate([Good(1)], [Game]), DiscoveryDecisionKind.InsufficientEvidence,
            DiscoveryReason.AwaitingIndependentEpisode);

    [Fact]
    public void No_episode_awaits_an_independent_episode() =>
        AssertDecision(Evaluate([], [Game]), DiscoveryDecisionKind.InsufficientEvidence,
            DiscoveryReason.AwaitingIndependentEpisode);

    [Fact]
    public void Empty_complete_inventory_refuses_no_candidates() =>
        AssertDecision(Evaluate([Good(1), Good(2)], []), DiscoveryDecisionKind.InsufficientEvidence,
            DiscoveryReason.NoCandidates);

    [Fact]
    public void Absent_installation_refuses_before_evidence()
    {
        var absent = NewScope(present: false);
        AssertDecision(Evaluate([Good(1), Good(2)], [Game], absent),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.InstallationAbsent);
    }

    [Fact]
    public void Ambiguous_installation_refuses_before_evidence() =>
        AssertDecision(Evaluate([Good(1), Good(2)], [Game], ambiguousInstallation: true),
            DiscoveryDecisionKind.Ambiguous, DiscoveryReason.AmbiguousInstallation);

    [Fact]
    public void Incomplete_inventory_even_with_candidates_refuses() =>
        AssertDecision(Evaluate([Good(1), Good(2)], [Game], completeness: InventoryCompleteness.Incomplete),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.IncompleteInventory);

    [Theory]
    [InlineData(ProcessSignatureOrigin.Manual)]
    [InlineData(ProcessSignatureOrigin.BuiltIn)]
    public void Explicit_signatures_are_protected_even_with_good_evidence(ProcessSignatureOrigin origin) =>
        AssertDecision(Evaluate([Good(1), Good(2)], [Game], origin: origin),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.ProtectedSignature);

    [Fact]
    public void Existing_discovered_allows_a_pure_proposal() =>
        AssertDecision(Evaluate([Good(1), Good(2)], [Game], origin: ProcessSignatureOrigin.Discovered),
            DiscoveryDecisionKind.PromoteMain, DiscoveryReason.RepeatedQualifiedEpisodes, Game);

    [Fact]
    public void Explicit_partial_quality_refuses() =>
        AssertDecision(Evaluate([Good(1), Change(Good(2), quality: EpisodeQuality.Partial)], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.PartialEpisode);

    [Theory]
    [InlineData(1, 7)]
    [InlineData(2, 8)]
    public void One_absent_capture_at_either_end_is_partial(long first, long last) =>
        AssertDecision(Evaluate([Good(1), Episode(2, Evidence(Game, new SnapshotRange(first, last)))], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.PartialEpisode);

    [Fact]
    public void Two_global_internal_absences_cannot_merge_launches() =>
        AssertDecision(Evaluate([Good(1), Episode(2, Evidence(Game,
            new SnapshotRange(2, 3), new SnapshotRange(6, 7)))], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.PartialEpisode);

    [Fact]
    public void Two_global_absences_between_separate_main_ranges_refuse_before_presence() =>
        AssertDecision(Evaluate([Good(1), Episode(2, Evidence(Game,
            new SnapshotRange(3, 4), new SnapshotRange(7, 7)),
            Evidence(Companion, new SnapshotRange(2, 4)))], [Game, Companion]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.PartialEpisode);

    [Fact]
    public void Capture_gap_quality_refuses() =>
        AssertDecision(Evaluate([Good(1), Change(Good(2), quality: EpisodeQuality.CaptureGap)], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.CaptureGap);

    [Fact]
    public void Unknown_process_identity_quality_refuses() =>
        AssertDecision(Evaluate([Good(1), Change(Good(2), quality: EpisodeQuality.UnknownProcessIdentity)], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.UnknownProcessIdentity);

    [Fact]
    public void Unreliable_candidate_identity_refuses() =>
        AssertDecision(Evaluate([Good(1), Episode(2, Custom(Game, Revision, identity: false,
            ranges: [new SnapshotRange(2, 7)]))], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.UnknownProcessIdentity);

    [Fact]
    public void Unreliable_candidate_path_refuses() =>
        AssertDecision(Evaluate([Good(1), Episode(2, Custom(Game, Revision, path: false,
            ranges: [new SnapshotRange(2, 7)]))], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.UnreliablePath);

    [Fact]
    public void Evidence_outside_inventory_is_not_ignored() =>
        AssertDecision(Evaluate([Good(1), Episode(2, Evidence(Game, new SnapshotRange(2, 7)),
            Evidence(Third, new SnapshotRange(2, 3)))], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.UnreliablePath);

    [Fact]
    public void Null_observed_revision_refuses() =>
        AssertDecision(Evaluate([Good(1), Episode(2, Custom(Game, revision: null,
            ranges: [new SnapshotRange(2, 7)]))], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.RevisionChanged);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Changed_size_or_write_time_refuses(bool changeSize)
    {
        var changed = changeSize ? new FileRevision(11, T0) : new FileRevision(10, T0.AddSeconds(1));
        AssertDecision(Evaluate([Good(1), Episode(2, Custom(Game, revision: changed,
            ranges: [new SnapshotRange(2, 7)]))], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.RevisionChanged);
    }

    [Fact]
    public void Main_seen_once_has_insufficient_presence() =>
        AssertDecision(Evaluate([Good(1), Episode(2, Evidence(Game, new SnapshotRange(2, 2)))], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.InsufficientPresence);

    [Fact]
    public void Two_equivalent_candidates_remain_ambiguous() =>
        AssertDecision(Evaluate([Episode(1, Evidence(Game, new SnapshotRange(2, 7)),
            Evidence(Companion, new SnapshotRange(2, 7)))], [Game, Companion]),
            DiscoveryDecisionKind.Ambiguous, DiscoveryReason.EquivalentCandidates);

    [Fact]
    public void Coextensive_generic_anti_cheat_remains_ambiguous()
    {
        var antiCheat = new ExecutableCandidate(Third.ExecutablePath, "anti-cheat.exe", Revision);
        AssertDecision(Evaluate([Episode(1, Evidence(Game, new SnapshotRange(2, 7)),
            Evidence(antiCheat, new SnapshotRange(2, 7)))], [Game, antiCheat]),
            DiscoveryDecisionKind.Ambiguous, DiscoveryReason.EquivalentCandidates);
    }

    [Fact]
    public void Candidate_with_explicit_empty_evidence_is_ignored_without_runtime_presence() =>
        AssertDecision(Evaluate([Episode(1, Evidence(Game, new SnapshotRange(2, 7)),
            Evidence(Companion))], [Game, Companion]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.AwaitingIndependentEpisode);

    [Fact]
    public void Inventory_only_candidates_do_not_create_false_unobserved_competitor_ambiguity()
    {
        var launcher = new ExecutableCandidate(@"C:\Games\Example\launcher.exe", "launcher.exe", Revision);
        var helper = new ExecutableCandidate(@"C:\Games\Example\helper.exe", "helper.exe", Revision);
        var crashReporter = new ExecutableCandidate(@"C:\Games\Example\crash-reporter.exe", "crash-reporter.exe", Revision);
        var tool = new ExecutableCandidate(@"C:\Games\Example\tool.exe", "tool.exe", Revision);

        AssertDecision(Evaluate([
                Episode(1, Evidence(launcher, new SnapshotRange(2, 3)),
                    Evidence(Game, new SnapshotRange(4, 7))),
                Episode(2, Evidence(launcher, new SnapshotRange(2, 3)),
                    Evidence(Game, new SnapshotRange(4, 7)))],
            [launcher, Game, helper, crashReporter, tool]),
            DiscoveryDecisionKind.PromoteMain, DiscoveryReason.RepeatedQualifiedEpisodes, Game);
    }

    [Fact]
    public void Late_competitor_is_not_a_startup_companion() =>
        AssertDecision(Evaluate([Episode(1, Evidence(Game, new SnapshotRange(3, 7)),
            Evidence(Companion, new SnapshotRange(4, 5)))], [Game, Companion]),
            DiscoveryDecisionKind.Ambiguous, DiscoveryReason.LateCompetitor);

    [Fact]
    public void Companion_staying_alive_creates_equivalent_survivors() =>
        AssertDecision(Evaluate([Episode(1, Evidence(Game, new SnapshotRange(3, 7)),
            Evidence(Companion, new SnapshotRange(2, 7)))], [Game, Companion]),
            DiscoveryDecisionKind.Ambiguous, DiscoveryReason.EquivalentCandidates);

    [Fact]
    public void Companion_reappearing_is_not_explained() =>
        AssertDecision(Evaluate([Episode(1, Evidence(Game, new SnapshotRange(3, 7)),
            Evidence(Companion, new SnapshotRange(2, 3), new SnapshotRange(5, 5)))],
            [Game, Companion]), DiscoveryDecisionKind.Ambiguous,
            DiscoveryReason.ReappearingCompetitor);

    [Fact]
    public void Companion_leaving_only_one_absent_capture_with_main_is_equivalent() =>
        AssertDecision(Evaluate([Episode(1, Evidence(Game, new SnapshotRange(3, 7)),
            Evidence(Companion, new SnapshotRange(2, 6)))], [Game, Companion]),
            DiscoveryDecisionKind.Ambiguous, DiscoveryReason.EquivalentCandidates);

    [Fact]
    public void Candidate_order_does_not_choose_a_survivor() =>
        AssertDecision(Evaluate([Episode(1, Evidence(Companion, new SnapshotRange(2, 7)),
            Evidence(Game, new SnapshotRange(2, 7)))], [Companion, Game]),
            DiscoveryDecisionKind.Ambiguous, DiscoveryReason.EquivalentCandidates);

    [Fact]
    public void Two_qualifying_episodes_with_different_mains_conflict() =>
        AssertDecision(Evaluate([WithCompanion(1), Flip(2)], [Game, Companion]),
            DiscoveryDecisionKind.Ambiguous, DiscoveryReason.ConflictingEpisodes);

    [Fact]
    public void Success_contradiction_success_awaits_new_confirmation() =>
        AssertDecision(Evaluate([WithCompanion(1), Flip(2), WithCompanion(3)], [Game, Companion]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.AwaitingIndependentEpisode);

    [Fact]
    public void Success_quality_gap_success_awaits_new_confirmation() =>
        AssertDecision(Evaluate([Good(1), Change(Good(2), quality: EpisodeQuality.CaptureGap), Good(3)], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.AwaitingIndependentEpisode);

    [Fact]
    public void Success_contradiction_then_two_successes_promotes_new_pair() =>
        AssertDecision(Evaluate([WithCompanion(1), Flip(2), WithCompanion(3), WithCompanion(4)], [Game, Companion]),
            DiscoveryDecisionKind.PromoteMain, DiscoveryReason.RepeatedQualifiedEpisodes, Game);

    [Fact]
    public void Three_successive_successes_keep_the_last_as_reference() =>
        AssertDecision(Evaluate([Good(1), Good(2), Good(3)], [Game]),
            DiscoveryDecisionKind.PromoteMain, DiscoveryReason.RepeatedQualifiedEpisodes, Game);

    [Fact]
    public void Latest_quality_failure_invalidates_earlier_promotion() =>
        AssertDecision(Evaluate([Good(1), Good(2), Change(Good(3), quality: EpisodeQuality.Partial)], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.PartialEpisode);

    [Fact]
    public void Duplicate_episode_id_refuses_supplied_sequence()
    {
        var first = Good(1);
        var second = Episode(first.EpisodeId, 2, Scope,
            ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, T0.AddMinutes(2),
            T0.AddMinutes(2).AddSeconds(18), 0, 9, EpisodeQuality.Complete,
            Evidence(Game, new SnapshotRange(2, 7)));
        AssertDecision(Evaluate([first, second], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.DuplicateEpisode);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 1)]
    public void Sequence_number_gap_or_reversal_refuses(long first, long second) =>
        AssertDecision(Evaluate([Good(first), Good(second)], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.CaptureGap);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Overlapping_or_touching_episode_times_refuse(bool touching)
    {
        var first = Good(1);
        var start = touching ? first.EndedAtUtc : first.EndedAtUtc.AddSeconds(-1);
        AssertDecision(Evaluate([first, Change(Good(2), start: start)], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.OverlappingEpisodes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Different_game_installation_or_root_refuses_scope(int changed)
    {
        var other = changed switch
        {
            0 => NewScope(game: new GameId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))),
            1 => NewScope(installation: new InstallationId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))),
            _ => NewScope(root: @"C:\Games\Elsewhere")
        };
        AssertDecision(Evaluate([Good(1), Change(Good(2), scope: other)], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.ScopeChanged);
    }

    [Fact]
    public void Changed_generation_refuses() =>
        AssertDecision(Evaluate([Good(1), Change(Good(2), scope: NewScope(
            generation: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")))], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.GenerationChanged);

    [Fact]
    public void Changed_policy_version_refuses() =>
        AssertDecision(Evaluate([Good(1), Change(Good(2), version:
            ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion + 1)], [Game]),
            DiscoveryDecisionKind.InsufficientEvidence, DiscoveryReason.PolicyVersionChanged);

    [Fact]
    public void Local_capture_indices_do_not_define_episode_sequence() =>
        AssertDecision(Evaluate([Good(100), Good(101)], [Game]),
            DiscoveryDecisionKind.PromoteMain, DiscoveryReason.RepeatedQualifiedEpisodes, Game);

    [Fact]
    public void Repeated_evaluation_is_value_equal_and_does_not_mutate_inputs()
    {
        var episodes = new[] { Good(1), Good(2) };
        var before = episodes.Select(item => item.Candidates[0].PresenceRanges[0]).ToArray();
        var inventory = new ExecutableInventory(Scope, InventoryCompleteness.Complete, [Game], []);
        var evaluation = new DiscoveryEvaluation(inventory, episodes, false, null);
        var policy = new ProcessSignatureDiscoveryPolicy();
        var first = policy.Evaluate(evaluation);
        var second = policy.Evaluate(evaluation);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(first.Main, second.Main);
        Assert.Equal(first.Reasons, second.Reasons);
        Assert.Equal(before, episodes.Select(item => item.Candidates[0].PresenceRanges[0]));
    }

    [Fact]
    public void Candidate_order_permuted_keeps_the_same_promoted_main()
    {
        var episodes = new[] { Episode(1, Evidence(Companion, new SnapshotRange(2, 4)),
            Evidence(Game, new SnapshotRange(3, 7))), Episode(2,
            Evidence(Game, new SnapshotRange(3, 7)), Evidence(Companion, new SnapshotRange(2, 4))) };
        AssertDecision(Evaluate(episodes, [Companion, Game]),
            DiscoveryDecisionKind.PromoteMain, DiscoveryReason.RepeatedQualifiedEpisodes, Game);
    }

    [Fact]
    public void Snapshot_range_arithmetic_near_long_max_value_remains_valid()
    {
        var first = Episode(Guid.NewGuid(), 1, Scope, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
            T0.AddMinutes(1), T0.AddMinutes(1).AddSeconds(18), long.MaxValue - 9,
            long.MaxValue, EpisodeQuality.Complete,
            Evidence(Game, new SnapshotRange(long.MaxValue - 7, long.MaxValue - 2)));
        var second = Episode(Guid.NewGuid(), 2, Scope, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
            T0.AddMinutes(2), T0.AddMinutes(2).AddSeconds(18), long.MaxValue - 9,
            long.MaxValue, EpisodeQuality.Complete,
            Evidence(Game, new SnapshotRange(long.MaxValue - 7, long.MaxValue - 2)));
        AssertDecision(Evaluate([first, second], [Game]),
            DiscoveryDecisionKind.PromoteMain, DiscoveryReason.RepeatedQualifiedEpisodes, Game);
    }

    private static LearningEpisodeSummary WithCompanion(long sequence) => Episode(sequence,
        Evidence(Game, new SnapshotRange(3, 7)), Evidence(Companion, new SnapshotRange(2, 4)));

    private static LearningEpisodeSummary Flip(long sequence) => Episode(sequence,
        Evidence(Companion, new SnapshotRange(3, 7)), Evidence(Game, new SnapshotRange(2, 4)));

    private static LearningEpisodeSummary Change(LearningEpisodeSummary original,
        InstallationScope? scope = null, int? version = null, DateTimeOffset? start = null,
        DateTimeOffset? end = null, long? first = null, long? last = null,
        EpisodeQuality? quality = null) => Episode(original.EpisodeId, original.SequenceNumber,
        scope ?? original.Scope, version ?? original.PolicyVersion, start ?? original.StartedAtUtc,
        end ?? original.EndedAtUtc, first ?? original.FirstSnapshot, last ?? original.LastSnapshot,
        quality ?? original.Quality, original.Candidates.ToArray());

    private static CandidateEpisodeEvidence Custom(ExecutableCandidate candidate,
        FileRevision? revision = default, bool path = true, bool identity = true,
        SnapshotRange[]? ranges = null) => new(candidate.ExecutablePath, revision,
            path, identity, ranges ?? []);

    private static LearningEpisodeSummary Good(long sequence) =>
        Episode(sequence, Evidence(Game, new SnapshotRange(2, 7)));

    private static InstallationScope NewScope(GameId? game = null, InstallationId? installation = null,
        string? root = null, Guid? generation = null, bool present = true) => new(
        game ?? Scope.GameId, installation ?? Scope.InstallationId, root ?? Scope.RootPath,
        generation ?? Scope.GenerationId, present);

    private static DiscoveryDecision Evaluate(IReadOnlyList<LearningEpisodeSummary> episodes,
        IReadOnlyList<ExecutableCandidate> candidates, InstallationScope? scope = null,
        InventoryCompleteness completeness = InventoryCompleteness.Complete,
        bool ambiguousInstallation = false, ProcessSignatureOrigin? origin = null)
    {
        var actualScope = scope ?? Scope;
        var issues = completeness == InventoryCompleteness.Complete
            ? Array.Empty<InventoryIssue>()
            : [new InventoryIssue(actualScope.RootPath, InventoryIssueKind.IoFailure)];
        var inventory = new ExecutableInventory(actualScope, completeness, candidates, issues);
        return new ProcessSignatureDiscoveryPolicy().Evaluate(
            new DiscoveryEvaluation(inventory, episodes, ambiguousInstallation, origin));
    }

    private static LearningEpisodeSummary Episode(long sequence, params CandidateEpisodeEvidence[] candidates) =>
        Episode(Guid.NewGuid(), sequence, Scope, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
            T0.AddMinutes(sequence), T0.AddMinutes(sequence).AddSeconds(18), 0, 9,
            EpisodeQuality.Complete, candidates);

    private static LearningEpisodeSummary Episode(Guid id, long sequence, InstallationScope scope,
        int version, DateTimeOffset start, DateTimeOffset end, long first, long last,
        EpisodeQuality quality, params CandidateEpisodeEvidence[] candidates) =>
        new(id, sequence, scope, version, start, end, first, last, quality, candidates);

    private static CandidateEpisodeEvidence Evidence(ExecutableCandidate candidate,
        params SnapshotRange[] ranges) => new(candidate.ExecutablePath, candidate.Revision, true, true, ranges);

    private static void AssertDecision(DiscoveryDecision actual, DiscoveryDecisionKind kind,
        DiscoveryReason reason, ExecutableCandidate? main = null)
    {
        Assert.Equal(kind, actual.Kind);
        Assert.Equal(main, actual.Main);
        Assert.Equal([reason], actual.Reasons);
    }
}
