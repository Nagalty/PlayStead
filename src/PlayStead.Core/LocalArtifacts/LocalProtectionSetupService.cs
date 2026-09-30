using PlayStead.Core.Library;

namespace PlayStead.Core.LocalArtifacts;

public enum LocalProtectionState
{
    NotProtected,
    BaselineOnly,
    Protected
}

public sealed record LocalProtectionGameContext(
    GameId GameId,
    ProviderKind? Provider,
    string? ProviderGameId,
    string? DisplayName = null);

public sealed record LocalProtectionArtifactSummary(
    GameLocalArtifact Artifact,
    LocalProtectionState State,
    int SnapshotCount,
    DateTimeOffset? LastSnapshotAtUtc);

public sealed record LocalProtectionInventory(
    IReadOnlyList<LocalProtectionArtifactSummary> Artifacts)
{
    public int RecognizedGamesCount => Artifacts.Select(x => x.Artifact.GameId).Distinct().Count();
    public int RecognizedArtifactsCount => Artifacts.Count;
    public int RecognizedSaveArtifactsCount => Artifacts.Count(x => x.Artifact.Kind == GameLocalArtifactKind.SaveData);
    public int AlreadyProtectedArtifactsCount => Artifacts.Count(x => x.State == LocalProtectionState.Protected);
    public int PendingProtectionArtifactsCount => Artifacts.Count(x => x.State != LocalProtectionState.Protected);
}

public sealed record LocalProtectionRunResult(int Protected, int AlreadyProtected, int Failed);

public interface ILocalProtectionSetupService
{
    Task<LocalProtectionInventory> InspectAsync(
        IReadOnlyList<LocalProtectionGameContext> games,
        CancellationToken cancellationToken);

    Task<LocalProtectionRunResult> ProtectAsync(
        IReadOnlyList<LocalProtectionGameContext> games,
        CancellationToken cancellationToken);
}

/// <summary>Creates only local fingerprints and safety snapshots for built-in rules.</summary>
public sealed class LocalProtectionSetupService(
    IGameLocalArtifactDiscoveryService discovery,
    IArtifactFingerprintService fingerprint,
    ILocalArtifactBaselineStore baselines,
    ILocalArtifactSnapshotService snapshots) : ILocalProtectionSetupService
{
    public async Task<LocalProtectionInventory> InspectAsync(
        IReadOnlyList<LocalProtectionGameContext> games,
        CancellationToken cancellationToken)
    {
        var summaries = new List<LocalProtectionArtifactSummary>();
        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var artifacts = await DiscoverBuiltInAsync(game, cancellationToken).ConfigureAwait(false);
            foreach (var artifact in artifacts)
                summaries.Add(await SummarizeAsync(artifact, cancellationToken).ConfigureAwait(false));
        }
        return new LocalProtectionInventory(summaries);
    }

    public async Task<LocalProtectionRunResult> ProtectAsync(
        IReadOnlyList<LocalProtectionGameContext> games,
        CancellationToken cancellationToken)
    {
        var protectedCount = 0;
        var alreadyProtected = 0;
        var failed = 0;
        foreach (var game in games)
        {
            var artifacts = await DiscoverBuiltInAsync(game, cancellationToken).ConfigureAwait(false);
            foreach (var artifact in artifacts)
            {
                try
                {
                    var before = await SummarizeAsync(artifact, cancellationToken).ConfigureAwait(false);
                    if (before.State == LocalProtectionState.Protected)
                    {
                        alreadyProtected++;
                        continue;
                    }

                    if (!artifact.Exists || artifact.RuleIdentity is null)
                    {
                        failed++;
                        continue;
                    }

                    var current = await fingerprint.ComputeAsync(artifact, cancellationToken).ConfigureAwait(false);
                    if (!current.IsAvailable)
                    {
                        failed++;
                        continue;
                    }

                    var existingBaseline = await baselines.GetAsync(artifact.GameId, artifact.Kind, artifact.RuleIdentity, cancellationToken).ConfigureAwait(false);
                    if (existingBaseline is null)
                    {
                        var value = current.Fingerprint!;
                        await baselines.UpsertAsync(new LocalArtifactBaseline(
                            artifact.GameId, artifact.Kind, artifact.RuleIdentity, value.Algorithm, value.Hash,
                            value.FileCount, value.TotalSizeBytes, value.CapturedAtUtc), cancellationToken).ConfigureAwait(false);
                    }

                    if (artifact.Kind == GameLocalArtifactKind.SaveData)
                    {
                        var withBaseline = artifact with { BaselineStatus = LocalArtifactBaselineStatus.Unchanged };
                        var currentSnapshots = await snapshots.ListAsync(withBaseline, cancellationToken).ConfigureAwait(false);
                        if (currentSnapshots.Count == 0)
                            await snapshots.CreateAsync(withBaseline, cancellationToken, SnapshotReason.InitialProtection).ConfigureAwait(false);
                    }

                    protectedCount++;
                }
                catch
                {
                    failed++;
                }
            }
        }
        return new LocalProtectionRunResult(protectedCount, alreadyProtected, failed);
    }

    private async Task<IReadOnlyList<GameLocalArtifact>> DiscoverBuiltInAsync(LocalProtectionGameContext game, CancellationToken cancellationToken)
        => (await discovery.DiscoverAsync(game.GameId, game.Provider, game.ProviderGameId, cancellationToken).ConfigureAwait(false))
            .Where(x => x.Source != GameLocalArtifactSource.UserDefined)
            .Where(x => x.Kind is GameLocalArtifactKind.Configuration or GameLocalArtifactKind.SaveData)
            .ToArray();

    private async Task<LocalProtectionArtifactSummary> SummarizeAsync(GameLocalArtifact artifact, CancellationToken cancellationToken)
    {
        var baseline = artifact.RuleIdentity is null
            ? null
            : await baselines.GetAsync(artifact.GameId, artifact.Kind, artifact.RuleIdentity, cancellationToken).ConfigureAwait(false);
        var listed = artifact.RuleIdentity is null
            ? []
            : await snapshots.ListAsync(artifact with { BaselineStatus = baseline is null ? LocalArtifactBaselineStatus.NoBaseline : LocalArtifactBaselineStatus.Unchanged }, cancellationToken).ConfigureAwait(false);
        var state = baseline is null
            ? LocalProtectionState.NotProtected
            : artifact.Kind == GameLocalArtifactKind.SaveData && listed.Count == 0
                ? LocalProtectionState.BaselineOnly
                : LocalProtectionState.Protected;
        return new LocalProtectionArtifactSummary(artifact, state, listed.Count, listed.Count == 0 ? null : listed.Max(x => x.CreatedAtUtc));
    }
}
