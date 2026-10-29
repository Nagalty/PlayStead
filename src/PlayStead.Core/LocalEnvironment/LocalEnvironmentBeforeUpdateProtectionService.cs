using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.Core.LocalEnvironment;

/// <summary>Protects eligible configuration files immediately before a provider update is applied.</summary>
public sealed class LocalEnvironmentBeforeUpdateProtectionService(
    ILibraryStore libraryStore,
    ILocalEnvironmentDiscoveryService discovery,
    ILocalEnvironmentSnapshotService snapshots,
    IBeforeUpdateProtectionPolicyStore policies) : IBeforeUpdateProtectionWorkflow
{
    private readonly object _gate = new();
    private readonly HashSet<string> _handledUpdates = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, PendingWork> _pending = [];
    private readonly List<string> _failures = [];

    public IReadOnlyList<BeforeUpdateProtectionPrompt> PendingPrompts
    {
        get { lock (_gate) return _pending.Values.Select(x => x.Prompt).ToArray(); }
    }

    public string? LastFailureMessage { get { lock (_gate) return _failures.LastOrDefault(); } }
    public string? LastStatusMessage { get; private set; }
    public event EventHandler? Changed;

    public async Task HandleUpdateAsync(ProviderInstallUpdateState state, CancellationToken cancellationToken)
    {
        if (state.Status is not (ProviderInstallUpdateStatus.UpdateAvailable))
        {
            if (state.Status is ProviderInstallUpdateStatus.Downloading or ProviderInstallUpdateStatus.Staging)
                LastStatusMessage = "Protection non effectuée : mise à jour déjà en cours.";
            return;
        }
        if (string.IsNullOrWhiteSpace(state.TargetBuildId) || string.IsNullOrWhiteSpace(state.InstalledBuildId) || string.Equals(state.TargetBuildId, state.InstalledBuildId, StringComparison.Ordinal)) return;

        var installation = (await libraryStore.LoadSnapshotAsync(cancellationToken)).Installations
            .Where(x => x.GameId == state.GameId && x.Provider == state.Provider && x.IsPresent)
            .FirstOrDefault(x => string.Equals(x.ExternalId, state.ProviderGameId, StringComparison.OrdinalIgnoreCase));
        if (installation is null) return;

        var key = $"{installation.Id.Value:D}:{state.TargetBuildId}";
        lock (_gate) if (!_handledUpdates.Add(key)) return;

        var entries = await GetEligibleAsync(installation.Id, cancellationToken);
        if (entries.Count == 0) return;
        var policy = await policies.GetAsync(installation.Id, cancellationToken);
        if (policy == BeforeUpdateProtectionPolicy.Never) return;
        var title = (await libraryStore.LoadSnapshotAsync(cancellationToken)).Games.FirstOrDefault(x => x.Id == state.GameId)?.Title ?? state.ProviderGameId;
        if (policy == BeforeUpdateProtectionPolicy.Ask)
        {
            var prompt = new BeforeUpdateProtectionPrompt(Guid.NewGuid(), installation.Id, state.GameId.Value, title, state.TargetBuildId, entries.Count);
            lock (_gate) _pending[prompt.Id] = new PendingWork(prompt, entries, key);
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        await CreateAsync(entries, installation.Id, key, cancellationToken);
    }

    public async Task<bool> AcceptAsync(Guid promptId, CancellationToken cancellationToken)
    {
        PendingWork? work;
        lock (_gate) { if (!_pending.Remove(promptId, out work)) return false; }
        await CreateAsync(work.Entries, work.Prompt.InstallationId, work.UpdateKey, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Decline(Guid promptId)
    {
        lock (_gate) if (!_pending.Remove(promptId)) return false;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private async Task<IReadOnlyList<LocalEnvironmentEntry>> GetEligibleAsync(InstallationId installationId, CancellationToken cancellationToken)
    {
        await discovery.RefreshEnvironmentAsync(installationId, cancellationToken);
        var environment = await discovery.GetEnvironmentAsync(installationId, cancellationToken);
        return environment.Entries.Where(x => SnapshotEligibilityPolicy.Evaluate(x).IsEligible).ToArray();
    }

    private async Task CreateAsync(IReadOnlyList<LocalEnvironmentEntry> entries, InstallationId installationId, string updateKey, CancellationToken cancellationToken)
    {
        var protectedCount = 0;
        foreach (var entry in entries)
        {
            try
            {
                await snapshots.CreateSnapshotAsync(installationId, entry.Id, LocalFileSnapshotReason.BeforeUpdate, cancellationToken);
                protectedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { lock (_gate) _failures.Add(ex.Message); }
        }
        LastStatusMessage = protectedCount == entries.Count ? $"{protectedCount} réglage{(protectedCount == 1 ? string.Empty : "s")} protégé{(protectedCount == 1 ? string.Empty : "s")} avant la mise à jour." : "Certains réglages n’ont pas pu être protégés.";
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed record PendingWork(BeforeUpdateProtectionPrompt Prompt, IReadOnlyList<LocalEnvironmentEntry> Entries, string UpdateKey);
}
