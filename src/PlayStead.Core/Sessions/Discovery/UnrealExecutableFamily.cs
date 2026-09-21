namespace PlayStead.Core.Sessions.Discovery;

internal sealed class UnrealExecutableFamily
{
    private const string ShippingSuffix = "-Win64-Shipping.exe";
    private readonly ExecutableInventory _inventory;
    private readonly string _project;

    private UnrealExecutableFamily(ExecutableInventory inventory, string project,
        ExecutableCandidate root, ExecutableCandidate shipping)
    {
        _inventory = inventory;
        _project = project;
        Root = root;
        Shipping = shipping;
    }

    public ExecutableCandidate Root { get; }
    public ExecutableCandidate Shipping { get; }

    public static UnrealExecutableFamily? TryCreate(ExecutableInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var pairs = new List<UnrealExecutableFamily>();
        foreach (var shipping in inventory.Candidates)
        {
            if (!shipping.ExecutableName.EndsWith(ShippingSuffix,
                    StringComparison.OrdinalIgnoreCase))
                continue;
            var project = shipping.ExecutableName[..^ShippingSuffix.Length];
            if (project.Length == 0 || !RelativePath(inventory, shipping).Equals(
                    $@"{project}\Binaries\Win64\{shipping.ExecutableName}",
                    StringComparison.OrdinalIgnoreCase))
                continue;
            var root = inventory.Candidates.SingleOrDefault(candidate =>
                RelativePath(inventory, candidate).Equals(project + ".exe",
                    StringComparison.OrdinalIgnoreCase));
            if (root is not null)
                pairs.Add(new UnrealExecutableFamily(inventory, project, root, shipping));
        }
        return pairs.Count == 1 ? pairs[0] : null;
    }

    public bool IsSupportExecutable(ExecutableCandidate candidate) =>
        ExecutableSupportClassifier.IsSupportExecutable(_inventory, candidate, this);

    internal bool IsProjectSupportExecutable(ExecutableCandidate candidate)
    {
        var relative = RelativePath(_inventory, candidate);
        if (relative.Length == 0) return false;
        if (relative.Equals($@"{_project}\Binaries\Win64\{_project}_BE.exe",
                StringComparison.OrdinalIgnoreCase))
            return true;
        if (relative.StartsWith($@"{_project}\Binaries\Win64\BattlEye\",
                StringComparison.OrdinalIgnoreCase) &&
            candidate.ExecutableName.StartsWith("BEService", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    public DiscoveryEvaluation Project(DiscoveryEvaluation evaluation)
    {
        var candidates = evaluation.Inventory.Candidates
            .Where(candidate => candidate != Root && !IsSupportExecutable(candidate))
            .ToArray();
        var inventory = new ExecutableInventory(evaluation.Inventory.Scope,
            evaluation.Inventory.Completeness, candidates, evaluation.Inventory.Issues);
        var episodes = evaluation.Episodes.Select(Project).ToArray();
        return new DiscoveryEvaluation(inventory, episodes,
            evaluation.HasAmbiguousInstallation, evaluation.ExistingSignatureOrigin);
    }

    private LearningEpisodeSummary Project(LearningEpisodeSummary episode)
    {
        var projected = episode.Candidates
            .Where(evidence => !IsFamilyOrSupport(evidence.ExecutablePath))
            .ToList();
        var familyEvidence = episode.Candidates
            .Where(evidence => SamePath(evidence.ExecutablePath, Root.ExecutablePath) ||
                SamePath(evidence.ExecutablePath, Shipping.ExecutablePath))
            .ToArray();
        var observed = familyEvidence.Where(evidence => evidence.PresenceRanges.Count != 0)
            .ToArray();
        projected.Add(new CandidateEpisodeEvidence(Shipping.ExecutablePath, Shipping.Revision,
            observed.All(evidence => evidence.HasReliablePath),
            observed.All(evidence => evidence.HasReliableIdentity),
            MergeRanges(observed.SelectMany(evidence => evidence.PresenceRanges))));
        return new LearningEpisodeSummary(episode.EpisodeId, episode.SequenceNumber,
            episode.Scope, episode.PolicyVersion, episode.StartedAtUtc, episode.EndedAtUtc,
            episode.FirstSnapshot, episode.LastSnapshot, episode.Quality, projected);
    }

    private bool IsFamilyOrSupport(string executablePath)
    {
        if (SamePath(executablePath, Root.ExecutablePath) ||
            SamePath(executablePath, Shipping.ExecutablePath)) return true;
        var candidate = _inventory.Candidates.FirstOrDefault(item =>
            SamePath(item.ExecutablePath, executablePath));
        return candidate is not null && IsSupportExecutable(candidate);
    }

    private static IReadOnlyList<SnapshotRange> MergeRanges(IEnumerable<SnapshotRange> ranges)
    {
        var ordered = ranges.OrderBy(range => range.First).ThenBy(range => range.Last).ToArray();
        if (ordered.Length == 0) return [];
        var merged = new List<SnapshotRange> { ordered[0] };
        foreach (var range in ordered.Skip(1))
        {
            var prior = merged[^1];
            if (range.First <= prior.Last + 1)
                merged[^1] = new SnapshotRange(prior.First, Math.Max(prior.Last, range.Last));
            else
                merged.Add(range);
        }
        return merged;
    }

    private static string RelativePath(ExecutableInventory inventory,
        ExecutableCandidate candidate)
    {
        var root = inventory.Scope.RootPath.TrimEnd('\\', '/');
        var prefix = root + "\\";
        if (!candidate.ExecutablePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return candidate.ExecutablePath[prefix.Length..].Replace('/', '\\');
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
