using PlayStead.Core.Library;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Platform.Processes.Discovery;

namespace PlayStead.Platform.Tests.Processes.Discovery;

public sealed class ExecutableInventoryPolicyIntegrationTests
{
    [Fact]
    public async Task Stable_single_executable_with_two_complete_episodes_proposes_exact_main()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var path = directory.Write("game.exe", [1, 2, 3]);
        var inventory = await new WindowsExecutableInventorySource()
            .InventoryAsync(Scope(directory.Root), CancellationToken.None);
        var candidate = Assert.Single(inventory.Candidates);

        var decision = Evaluate(inventory, TwoEpisodes(inventory.Scope, candidate));

        Assert.Equal(InventoryCompleteness.Complete, inventory.Completeness);
        Assert.Equal(DiscoveryDecisionKind.PromoteMain, decision.Kind);
        Assert.Equal(path, decision.Main?.ExecutablePath);
        Assert.Equal(candidate.Revision, decision.Main?.Revision);
    }

    [Fact]
    public async Task Stable_unobserved_second_executable_blocks_promotion()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        directory.Write("game.exe", [1]);
        directory.Write("other.exe", [2]);
        var inventory = await new WindowsExecutableInventorySource()
            .InventoryAsync(Scope(directory.Root), CancellationToken.None);
        var candidate = inventory.Candidates.Single(item => item.ExecutableName == "game.exe");

        var decision = Evaluate(inventory, TwoEpisodes(inventory.Scope, candidate));

        Assert.Equal(InventoryCompleteness.Complete, inventory.Completeness);
        Assert.Equal(2, inventory.Candidates.Count);
        Assert.Equal(DiscoveryDecisionKind.Ambiguous, decision.Kind);
        Assert.Contains(DiscoveryReason.UnobservedCompetitor, decision.Reasons);
        Assert.Null(decision.Main);
    }

    [Fact]
    public async Task Denied_executable_makes_even_good_episodes_insufficient()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var denied = directory.Write("denied.exe", [1]);
        var good = directory.Write("good.exe", [2]);
        var source = Source(readRevision: path =>
            path == denied ? throw new UnauthorizedAccessException() : Revision(path));
        var inventory = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);
        var candidate = Assert.Single(inventory.Candidates);

        var decision = Evaluate(inventory, TwoEpisodes(inventory.Scope, candidate));

        Assert.Equal(good, candidate.ExecutablePath);
        Assert.Equal(InventoryCompleteness.Incomplete, inventory.Completeness);
        Assert.Contains(inventory.Issues, issue => issue.Path == denied && issue.Kind == InventoryIssueKind.AccessDenied);
        Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, decision.Kind);
        Assert.Contains(DiscoveryReason.IncompleteInventory, decision.Reasons);
        Assert.Null(decision.Main);
    }

    [Fact]
    public async Task Changed_during_inventory_keeps_old_candidate_but_never_reaches_promotion()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var path = directory.Write("game.exe", [1, 2, 3]);
        var reads = 0;
        var source = Source(readRevision: entry =>
        {
            var oldRevision = Revision(entry);
            if (++reads == 1)
            {
                using var append = new FileStream(entry, FileMode.Append, FileAccess.Write);
                append.WriteByte(4);
            }
            return oldRevision;
        });
        var inventory = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);
        var candidate = Assert.Single(inventory.Candidates);

        var decision = Evaluate(inventory, TwoEpisodes(inventory.Scope, candidate));

        Assert.Equal(path, candidate.ExecutablePath);
        Assert.Equal(3, candidate.Revision.SizeBytes);
        Assert.Equal(InventoryCompleteness.Incomplete, inventory.Completeness);
        Assert.Contains(inventory.Issues, issue => issue.Path == path && issue.Kind == InventoryIssueKind.RevisionChanged);
        Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, decision.Kind);
        Assert.Contains(DiscoveryReason.IncompleteInventory, decision.Reasons);
        Assert.Null(decision.Main);
    }

    [Fact]
    public async Task Disappeared_at_final_revision_read_never_reaches_promotion()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var path = directory.Write("game.exe", [1, 2, 3]);
        var reads = 0;
        var source = Source(readRevision: entry =>
        {
            if (++reads == 2)
            {
                File.Delete(entry);
                throw new FileNotFoundException("Changed during walk.", entry);
            }
            return Revision(entry);
        });
        var inventory = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);
        var candidate = Assert.Single(inventory.Candidates);

        var decision = Evaluate(inventory, TwoEpisodes(inventory.Scope, candidate));

        Assert.Equal(InventoryCompleteness.Incomplete, inventory.Completeness);
        Assert.Contains(inventory.Issues, issue => issue.Path == path && issue.Kind == InventoryIssueKind.IoFailure);
        Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, decision.Kind);
        Assert.Contains(DiscoveryReason.IncompleteInventory, decision.Reasons);
        Assert.Null(decision.Main);
    }

    [Fact]
    public async Task Cancellation_during_final_revision_read_propagates()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        directory.Write("game.exe", [1]);
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        var source = Source(readRevision: path =>
        {
            if (++reads == 2)
                cancellation.Cancel();
            return Revision(path);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.InventoryAsync(Scope(directory.Root), cancellation.Token));
    }

    [Fact]
    public async Task Candidate_becoming_reparse_before_publication_is_not_read_again()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var path = directory.Write("game.exe", [1]);
        var candidateAttributeReads = 0;
        var revisionReads = 0;
        var source = new WindowsExecutableInventorySource(
            Directory.EnumerateFileSystemEntries,
            entry => entry == path && ++candidateAttributeReads == 2
                ? FileAttributes.ReparsePoint
                : File.GetAttributes(entry),
            entry =>
            {
                revisionReads++;
                return Revision(entry);
            });

        var inventory = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);
        var candidate = Assert.Single(inventory.Candidates);
        var decision = Evaluate(inventory, TwoEpisodes(inventory.Scope, candidate));

        Assert.Equal(1, revisionReads);
        Assert.Equal(InventoryCompleteness.Incomplete, inventory.Completeness);
        Assert.Contains(inventory.Issues, issue => issue.Path == path && issue.Kind == InventoryIssueKind.ReparsePoint);
        Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, decision.Kind);
        Assert.Null(decision.Main);
    }

    [Fact]
    public async Task Traversed_directory_becoming_reparse_blocks_publication()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var path = directory.Write(@"bin\game.exe", [1]);
        var bin = Path.GetDirectoryName(path)!;
        var binAttributeReads = 0;
        var source = new WindowsExecutableInventorySource(
            Directory.EnumerateFileSystemEntries,
            entry => entry == bin && ++binAttributeReads == 3
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : File.GetAttributes(entry), Revision);

        var inventory = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);
        var candidate = Assert.Single(inventory.Candidates);
        var decision = Evaluate(inventory, TwoEpisodes(inventory.Scope, candidate));

        Assert.Equal(InventoryCompleteness.Incomplete, inventory.Completeness);
        Assert.Contains(inventory.Issues, issue => issue.Path == bin && issue.Kind == InventoryIssueKind.ReparsePoint);
        Assert.Equal(DiscoveryDecisionKind.InsufficientEvidence, decision.Kind);
        Assert.Null(decision.Main);
    }

    private static InstallationScope Scope(string root) =>
        new(GameId.New(), InstallationId.New(), root, Guid.NewGuid(), true);

    private static WindowsExecutableInventorySource Source(
        Func<string, FileRevision>? readRevision = null) =>
        new(Directory.EnumerateFileSystemEntries, File.GetAttributes, readRevision ?? Revision);

    private static FileRevision Revision(string path)
    {
        var info = new FileInfo(path);
        return new FileRevision(info.Length, new DateTimeOffset(info.LastWriteTimeUtc));
    }

    private static DiscoveryDecision Evaluate(ExecutableInventory inventory,
        IReadOnlyList<LearningEpisodeSummary> episodes) =>
        new ProcessSignatureDiscoveryPolicy().Evaluate(
            new DiscoveryEvaluation(inventory, episodes, false, null));

    private static IReadOnlyList<LearningEpisodeSummary> TwoEpisodes(
        InstallationScope scope, ExecutableCandidate candidate)
    {
        LearningEpisodeSummary Episode(long sequence) => new(
            Guid.NewGuid(), sequence, scope, ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion,
            DateTimeOffset.UnixEpoch.AddMinutes(sequence),
            DateTimeOffset.UnixEpoch.AddMinutes(sequence).AddSeconds(14),
            0, 7, EpisodeQuality.Complete,
            [new CandidateEpisodeEvidence(candidate.ExecutablePath, candidate.Revision,
                true, true, [new SnapshotRange(2, 5)])]);
        return [Episode(1), Episode(2)];
    }
}
