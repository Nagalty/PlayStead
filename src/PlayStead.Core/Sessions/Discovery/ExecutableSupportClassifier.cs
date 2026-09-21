namespace PlayStead.Core.Sessions.Discovery;

internal static class ExecutableSupportClassifier
{
    public static bool IsSupportExecutable(ExecutableInventory inventory,
        ExecutableCandidate candidate, UnrealExecutableFamily? unrealFamily = null) =>
        HasConsistentExecutableName(inventory, candidate) &&
        (IsGenericSupportExecutable(inventory, candidate) ||
         unrealFamily?.IsProjectSupportExecutable(candidate) == true);

    public static bool IsPathlessSupportObservation(ExecutableInventory inventory,
        UnrealExecutableFamily? unrealFamily, string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName)) return false;
        var matches = inventory.Candidates.Where(candidate => string.Equals(
            candidate.ExecutableName, executableName, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length != 0 && matches.All(candidate =>
            IsSupportExecutable(inventory, candidate, unrealFamily));
    }

    public static DiscoveryEvaluation Project(DiscoveryEvaluation evaluation)
    {
        var supportPaths = evaluation.Inventory.Candidates
            .Where(candidate => IsGenericSupportExecutable(evaluation.Inventory, candidate))
            .Select(candidate => candidate.ExecutablePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (supportPaths.Count == 0) return evaluation;

        var inventory = new ExecutableInventory(evaluation.Inventory.Scope,
            evaluation.Inventory.Completeness,
            evaluation.Inventory.Candidates.Where(candidate =>
                !supportPaths.Contains(candidate.ExecutablePath)).ToArray(),
            evaluation.Inventory.Issues);
        var episodes = evaluation.Episodes.Select(episode => new LearningEpisodeSummary(
            episode.EpisodeId, episode.SequenceNumber, episode.Scope, episode.PolicyVersion,
            episode.StartedAtUtc, episode.EndedAtUtc, episode.FirstSnapshot,
            episode.LastSnapshot, episode.Quality,
            episode.Candidates.Where(candidate => !supportPaths.Contains(candidate.ExecutablePath))
                .ToArray())).ToArray();
        return new DiscoveryEvaluation(inventory, episodes,
            evaluation.HasAmbiguousInstallation, evaluation.ExistingSignatureOrigin);
    }

    private static bool IsGenericSupportExecutable(ExecutableInventory inventory,
        ExecutableCandidate candidate)
    {
        var relative = RelativePath(inventory, candidate);
        if (relative.Length == 0) return false;
        var fileName = relative[(relative.LastIndexOf('\\') + 1)..];
        if (!string.Equals(candidate.ExecutableName, fileName, StringComparison.OrdinalIgnoreCase))
            return false;
        if (relative.Equals(@"Engine\Binaries\Win64\CrashReportClient.exe",
                StringComparison.OrdinalIgnoreCase) ||
            relative.Equals(@"Engine\Binaries\Win64\EpicWebHelper.exe",
                StringComparison.OrdinalIgnoreCase) ||
            relative.Equals(@"Engine\Binaries\Win64\UnrealCEFSubProcess.exe",
                StringComparison.OrdinalIgnoreCase) ||
            relative.Equals(@"Installers\EasyAntiCheat_EOS_Setup.exe",
                StringComparison.OrdinalIgnoreCase))
            return true;

        const string redistPrefix = @"Engine\Extras\Redist\";
        if (!relative.StartsWith(redistPrefix, StringComparison.OrdinalIgnoreCase))
            return false;
        return fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            (fileName.StartsWith("UEPrereqSetup_", StringComparison.OrdinalIgnoreCase) ||
             fileName.StartsWith("vc_redist.", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasConsistentExecutableName(ExecutableInventory inventory,
        ExecutableCandidate candidate)
    {
        var relative = RelativePath(inventory, candidate);
        return relative.Length != 0 && string.Equals(candidate.ExecutableName,
            relative[(relative.LastIndexOf('\\') + 1)..], StringComparison.OrdinalIgnoreCase);
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
}
