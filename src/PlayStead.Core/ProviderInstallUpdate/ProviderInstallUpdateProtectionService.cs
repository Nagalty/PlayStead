using PlayStead.Core.LocalArtifacts;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.ProviderInstallUpdate;

public interface IWindowsSilentNotificationSink
{
    Task ShowAsync(string title, string message, CancellationToken cancellationToken);
}

public interface IProviderInstallUpdateProtectionService
{
    Task ProtectBeforeUpdateAsync(ProviderInstallUpdateState state, CancellationToken cancellationToken);
}

/// <summary>Creates one pre-update snapshot only after a reliable install update was observed.</summary>
public sealed class ProviderInstallUpdateProtectionService(
    ILocalProtectionSetupService protectionSetup,
    ILocalArtifactSnapshotService snapshots,
    INotificationCenterService notifications,
    ILibraryStore libraryStore,
    IWindowsSilentNotificationSink? windowsNotifications = null) : IProviderInstallUpdateProtectionService
{
    private readonly object _gate = new();
    private readonly HashSet<string> _handledUpdates = new(StringComparer.Ordinal);

    public async Task ProtectBeforeUpdateAsync(ProviderInstallUpdateState state, CancellationToken cancellationToken)
    {
        if (state.Status != ProviderInstallUpdateStatus.UpdateAvailable ||
            string.IsNullOrWhiteSpace(state.TargetBuildId) ||
            string.IsNullOrWhiteSpace(state.InstalledBuildId) ||
            string.Equals(state.InstalledBuildId, state.TargetBuildId, StringComparison.Ordinal))
            return;

        var updateKey = $"{state.Provider}:{state.GameId.Value:D}:{state.TargetBuildId}";
        lock (_gate)
        {
            if (!_handledUpdates.Add(updateKey)) return;
        }

        var title = await GetGameTitleAsync(state, cancellationToken).ConfigureAwait(false);
        try
        {
            var inventory = await protectionSetup.InspectAsync([
                new LocalProtectionGameContext(state.GameId, state.Provider, state.ProviderGameId, title)
            ], cancellationToken).ConfigureAwait(false);
            var protectedArtifacts = inventory.Artifacts
                .Where(x => x.State == LocalProtectionState.Protected && x.Artifact.Exists && x.Artifact.RuleIdentity is not null)
                .Select(x => x.Artifact with { BaselineStatus = LocalArtifactBaselineStatus.Unchanged })
                .ToArray();
            if (protectedArtifacts.Length == 0) return;

            foreach (var artifact in protectedArtifacts)
                await snapshots.CreateAsync(artifact, cancellationToken, SnapshotReason.PreUpdate).ConfigureAwait(false);

            var noun = protectedArtifacts.Any(x => x.Kind == GameLocalArtifactKind.SaveData)
                ? "tes sauvegardes"
                : "tes fichiers protégés";
            if (windowsNotifications is not null)
                await windowsNotifications.ShowAsync(
                    $"Mise à jour détectée pour « {title} »",
                    $"J’ai gardé une copie de {noun} avant qu’elle passe.",
                    cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await notifications.PublishOrRefreshAsync(
                new NotificationPublishRequest(
                    NotificationProducer.LocalProtection,
                    state.GameId.ToString(),
                    "pre-update-protection-failed",
                    new NotificationDeduplicationKey($"pre-update-protection:{updateKey}"),
                    NotificationPriority.ActionRequired,
                    $"Je n’ai pas pu protéger « {title} » avant sa mise à jour.",
                    "La protection avant mise à jour a échoué. Tu peux vérifier les fichiers locaux de ce jeu.",
                    null),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> GetGameTitleAsync(ProviderInstallUpdateState state, CancellationToken cancellationToken)
    {
        var snapshot = await libraryStore.LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.Games.FirstOrDefault(x => x.Id == state.GameId)?.Title ?? state.ProviderGameId;
    }
}
