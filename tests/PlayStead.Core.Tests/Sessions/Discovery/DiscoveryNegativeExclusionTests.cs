using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Tests.Sessions.Discovery;

public sealed class DiscoveryNegativeExclusionTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;
    private static readonly Guid Generation = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid FirstEpisode = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid SecondEpisode = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private const string Root = @"C:\Games\Example";
    private const string GamePath = @"C:\Games\Example\game.exe";

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("MySetupAdventure.exe")]
    [InlineData("CrashReportClient.exe")]
    [InlineData("server.exe")]
    [InlineData("UEPrereqSetup_x64.exe")]
    [InlineData("Game-Win64-Shipping.exe")]
    public void Filename_never_removes_an_unobserved_competitor(string name)
    {
        var scope = Scope();
        var revision = Revision();
        var game = new ExecutableCandidate(GamePath, "game.exe", revision);
        var other = new ExecutableCandidate(Root + @"\" + name, name, revision);
        var inventory = new ExecutableInventory(scope, InventoryCompleteness.Complete, [game, other], []);
        var evaluation = new DiscoveryEvaluation(inventory, [Episode(scope, 1), Episode(scope, 2)], false, null);

        var result = new ProcessSignatureDiscoveryPolicy().Evaluate(evaluation);

        Assert.Equal(DiscoveryDecisionKind.Ambiguous, result.Kind);
        Assert.Contains(DiscoveryReason.UnobservedCompetitor, result.Reasons);
        Assert.Null(result.Main);
    }

    [Theory]
    [InlineData(ProcessSignatureOrigin.Manual)]
    [InlineData(ProcessSignatureOrigin.BuiltIn)]
    public void Protected_signature_origin_refuses_discovery(ProcessSignatureOrigin origin)
    {
        var scope = Scope();
        var evaluation = new DiscoveryEvaluation(Inventory(scope), [Episode(scope, 1), Episode(scope, 2)], false, origin);

        var result = new ProcessSignatureDiscoveryPolicy().Evaluate(evaluation);

        Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, result.Kind);
        Assert.Contains(DiscoveryReason.ProtectedSignature, result.Reasons);
        Assert.Null(result.Main);
    }

    [Fact]
    public void Null_evaluation_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ProcessSignatureDiscoveryPolicy().Evaluate(null!));
    }

    [Fact]
    public void Inventory_without_an_episode_is_not_promoted()
    {
        var evaluation = new DiscoveryEvaluation(Inventory(Scope()), [], false, null);

        var result = new ProcessSignatureDiscoveryPolicy().Evaluate(evaluation);

        Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, result.Kind);
        Assert.Contains(DiscoveryReason.AwaitingIndependentEpisode, result.Reasons);
        Assert.Null(result.Main);
    }

    [Fact]
    public void Incomplete_inventory_refuses_even_with_two_episodes()
    {
        var scope = Scope();
        var inventory = new ExecutableInventory(scope, InventoryCompleteness.Incomplete,
            [new ExecutableCandidate(GamePath, "game.exe", Revision())],
            [new InventoryIssue(Root, InventoryIssueKind.IoFailure)]);
        var evaluation = new DiscoveryEvaluation(inventory, [Episode(scope, 1), Episode(scope, 2)], false, null);

        var result = new ProcessSignatureDiscoveryPolicy().Evaluate(evaluation);

        Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, result.Kind);
        Assert.Contains(DiscoveryReason.IncompleteInventory, result.Reasons);
        Assert.Null(result.Main);
    }

    private static InstallationScope Scope() => new(
        GameId.New(), InstallationId.New(), Root, Generation, true);

    private static FileRevision Revision() => new(10, Start);

    private static ExecutableInventory Inventory(InstallationScope scope) => new(
        scope, InventoryCompleteness.Complete,
        [new ExecutableCandidate(GamePath, "game.exe", Revision())], []);

    private static LearningEpisodeSummary Episode(InstallationScope scope, long sequence) => new(
        sequence == 1 ? FirstEpisode : SecondEpisode,
        sequence, scope, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
        Start.AddMinutes(sequence), Start.AddMinutes(sequence).AddSeconds(14),
        0, 7, EpisodeQuality.Complete,
        [new CandidateEpisodeEvidence(GamePath, Revision(), true, true, [new SnapshotRange(2, 5)])]);
}
