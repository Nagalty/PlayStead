using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Tests.Sessions.Discovery;

public sealed class UnrealExecutableFamilyDiscoveryTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;
    private static readonly FileRevision Revision = new(100, T0);

    [Fact]
    public void Pathless_family_evidence_semantics_use_policy_version_three()
    {
        Assert.Equal(3, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Unreal_family_promotes_shipping_member_from_repeated_family_evidence(
        bool rootObserved, bool shippingObserved)
    {
        var fixture = Fixture.Create("Test_C");

        var result = Evaluate(fixture, rootObserved, shippingObserved);

        Assert.Equal(DiscoveryDecisionKind.PromoteMain, result.Kind);
        Assert.Equal(fixture.Shipping.ExecutableName, result.Main?.ExecutableName);
        Assert.Equal(fixture.Shipping.ExecutablePath, result.Main?.ExecutablePath);
    }

    [Fact]
    public void Unreal_family_ignores_only_structural_support_executables()
    {
        var fixture = Fixture.Create("DuneSandbox",
        [
            @"DuneSandbox\Binaries\Win64\DuneSandbox_BE.exe",
            @"DuneSandbox\Binaries\Win64\BattlEye\BEService_x64.exe",
            @"Engine\Binaries\Win64\CrashReportClient.exe",
            @"Engine\Binaries\Win64\EpicWebHelper.exe",
            @"Engine\Binaries\Win64\UnrealCEFSubProcess.exe",
            @"Engine\Extras\Redist\en-us\UEPrereqSetup_x64.exe",
            @"Engine\Extras\Redist\vc_redist.x64.exe",
            @"Installers\EasyAntiCheat_EOS_Setup.exe"
        ]);

        var result = Evaluate(fixture, rootObserved: false, shippingObserved: true);

        Assert.Equal(DiscoveryDecisionKind.PromoteMain, result.Kind);
        Assert.Equal(fixture.Shipping.ExecutablePath, result.Main?.ExecutablePath);
    }

    [Fact]
    public void Generic_support_executables_are_not_unobserved_competitors_or_mains()
    {
        var scope = Scope(@"H:\SteamLibrary\steamapps\common\Sea of Thieves");
        var root = Candidate(scope, "SeaOfThieves.exe");
        var runtime = Candidate(scope, @"Athena\Binaries\Win64\SoTGame.exe");
        var cef = Candidate(scope, @"Engine\Binaries\Win64\UnrealCEFSubProcess.exe");
        var anticheat = Candidate(scope, @"Installers\EasyAntiCheat_EOS_Setup.exe");
        var candidates = new[] { root, runtime, cef, anticheat };
        LearningEpisodeSummary Episode(long sequence) => new(Guid.NewGuid(), sequence, scope,
            ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion, T0.AddMinutes(sequence),
            T0.AddMinutes(sequence).AddSeconds(14), 0, 9, EpisodeQuality.Complete,
            [
                new(root.ExecutablePath, root.Revision, true, true, [new SnapshotRange(2, 3)]),
                new(runtime.ExecutablePath, runtime.Revision, true, true, [new SnapshotRange(3, 7)]),
                new(cef.ExecutablePath, cef.Revision, true, true, []),
                new(anticheat.ExecutablePath, anticheat.Revision, true, true, [])
            ]);
        var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete, candidates, []);

        var result = new ProcessSignatureDiscoveryPolicy().Evaluate(new DiscoveryEvaluation(
            inventory, [Episode(1), Episode(2)], false, null));

        Assert.Equal(DiscoveryDecisionKind.PromoteMain, result.Kind);
        Assert.Equal(runtime, result.Main);
        Assert.DoesNotContain(result.Reasons, reason => reason == DiscoveryReason.UnobservedCompetitor);
        Assert.DoesNotContain(candidates.Where(candidate => candidate == cef || candidate == anticheat),
            candidate => candidate == result.Main);
    }

    [Theory]
    [InlineData(@"Tools\Unrelated.exe")]
    [InlineData(@"Installers\UnrelatedTool.exe")]
    public void Unrelated_unobserved_executable_remains_a_competitor(string unobservedPath)
    {
        var fixture = Fixture.Create("Test_C", [unobservedPath]);

        var result = Evaluate(fixture, rootObserved: false, shippingObserved: true);

        Assert.Equal(DiscoveryDecisionKind.Ambiguous, result.Kind);
        Assert.Contains(DiscoveryReason.UnobservedCompetitor, result.Reasons);
        Assert.Null(result.Main);
    }

    [Fact]
    public void Similar_names_without_the_strict_unreal_layout_remain_ambiguous()
    {
        var fixture = Fixture.Create("Test_C", includeCanonicalShipping: false);

        var result = Evaluate(fixture, rootObserved: true, shippingObserved: false);

        Assert.Equal(DiscoveryDecisionKind.Ambiguous, result.Kind);
        Assert.Contains(DiscoveryReason.UnobservedCompetitor, result.Reasons);
        Assert.Null(result.Main);
    }

    [Fact]
    public void Single_executable_control_keeps_existing_behavior()
    {
        var scope = Scope(@"C:\Games\Enshrouded");
        var game = Candidate(scope, "enshrouded.exe");
        var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete, [game], []);

        var result = new ProcessSignatureDiscoveryPolicy().Evaluate(new DiscoveryEvaluation(
            inventory, [Episode(scope, 1, [Evidence(game)]), Episode(scope, 2, [Evidence(game)])],
            false, null));

        Assert.Equal(DiscoveryDecisionKind.PromoteMain, result.Kind);
        Assert.Equal(game, result.Main);
    }

    [Fact]
    public void Accepted_shipping_identity_is_usable_by_path_aware_runtime_matcher()
    {
        var fixture = Fixture.Create("Test_C");
        var decision = Evaluate(fixture, rootObserved: false, shippingObserved: true);
        var signature = new ProcessSignature(fixture.Scope.GameId.Value,
        [
            new ProcessSignatureEntry(decision.Main!.ExecutableName,
                ProcessSignatureEntryKind.Main, decision.Main.ExecutablePath,
                decision.Main.Revision)
        ], ProcessSignatureOrigin.Discovered, T0,
            new DiscoveredSignatureMetadata(fixture.Scope.InstallationId,
                fixture.Scope.GenerationId, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
                ProcessSignatureValidationState.Valid, Guid.NewGuid()));

        var match = new ProcessSignatureMatcher().Match(signature,
        [
            new ProcessSnapshot(42, fixture.Shipping.ExecutableName,
                fixture.Shipping.ExecutablePath, T0)
        ]);

        Assert.True(match.HasMainProcess);
    }

    private static DiscoveryDecision Evaluate(Fixture fixture, bool rootObserved,
        bool shippingObserved)
    {
        IReadOnlyList<CandidateEpisodeEvidence> EvidenceForEpisode()
        {
            var evidence = fixture.Inventory.Candidates.Select(candidate =>
                new CandidateEpisodeEvidence(candidate.ExecutablePath, candidate.Revision,
                    true, true,
                    candidate == fixture.Root && rootObserved ||
                    candidate == fixture.Shipping && shippingObserved
                        ? [new SnapshotRange(2, 5)]
                        : [])).ToArray();
            return evidence;
        }

        return new ProcessSignatureDiscoveryPolicy().Evaluate(new DiscoveryEvaluation(
            fixture.Inventory,
            [Episode(fixture.Scope, 1, EvidenceForEpisode()),
                Episode(fixture.Scope, 2, EvidenceForEpisode())], false, null));
    }

    private static LearningEpisodeSummary Episode(InstallationScope scope, long sequence,
        IReadOnlyList<CandidateEpisodeEvidence> evidence) => new(
        Guid.NewGuid(), sequence, scope, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
        T0.AddMinutes(sequence), T0.AddMinutes(sequence).AddSeconds(14),
        0, 7, EpisodeQuality.Complete, evidence);

    private static CandidateEpisodeEvidence Evidence(ExecutableCandidate candidate) =>
        new(candidate.ExecutablePath, candidate.Revision, true, true,
            [new SnapshotRange(2, 5)]);

    private static ExecutableCandidate Candidate(InstallationScope scope, string relativePath) =>
        new(scope.RootPath + "\\" + relativePath, Path.GetFileName(relativePath), Revision);

    private static InstallationScope Scope(string root) => new(
        GameId.New(), InstallationId.New(), root, Guid.NewGuid(), true);

    private sealed record Fixture(InstallationScope Scope, ExecutableInventory Inventory,
        ExecutableCandidate Root, ExecutableCandidate Shipping)
    {
        public static Fixture Create(string project, IReadOnlyList<string>? extras = null,
            bool includeCanonicalShipping = true)
        {
            var scope = UnrealExecutableFamilyDiscoveryTests.Scope(@"C:\Games\Example");
            var root = Candidate(scope, project + ".exe");
            var shipping = Candidate(scope,
                includeCanonicalShipping
                    ? $@"{project}\Binaries\Win64\{project}-Win64-Shipping.exe"
                    : $@"Other\{project}-Win64-Shipping.exe");
            var candidates = new List<ExecutableCandidate> { root, shipping };
            if (extras is not null)
                candidates.AddRange(extras.Select(path => Candidate(scope, path)));
            return new Fixture(scope,
                new ExecutableInventory(scope, InventoryCompleteness.Complete, candidates, []),
                root, shipping);
        }
    }
}
