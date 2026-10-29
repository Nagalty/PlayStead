using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;
using PlayStead.Core.Persistence;
using PlayStead.Core.Configuration;

namespace PlayStead.UI.Library;

public sealed record LocalEnvironmentEntryViewModel(Guid Id, string Name, string StatusLabel, string Path, bool CanOpen, bool CanSnapshot);
public sealed record LocalEnvironmentSnapshotViewModel(Guid Id, Guid EnvironmentEntryId, string Name, string CreatedLabel, string ReasonLabel, string SizeLabel, bool IsPreRestoreBackup);
public sealed record BeforeUpdateProtectionPolicyOption(BeforeUpdateProtectionPolicy Value, string Label);

public sealed class LocalEnvironmentSectionViewModel : INotifyPropertyChanged
{
    private readonly ILibraryStore _libraryStore;
    private readonly ILocalEnvironmentDiscoveryService _discovery;
    private readonly ILocalEnvironmentBaselineStore _baselineStore;
    private readonly ILocalEnvironmentBaselineService? _baselineService;
    private readonly ILocalEnvironmentSnapshotService? _snapshotService;
    private InstallationId? _installationId;
    private readonly Func<string, Task<bool>> _confirmActionAsync;
    private readonly HashSet<Guid> _busySnapshotIds = [];
    private readonly HashSet<Guid> _busyEntryIds = [];
    private CancellationTokenSource? _loadCts;
    private readonly IBeforeUpdateProtectionPolicyStore? _policyStore;
    private readonly IBeforeUpdateProtectionWorkflow? _beforeUpdateWorkflow;
    private readonly IConfigSemanticService? _configSemanticService;
    private GameId? _gameId;

    public LocalEnvironmentSectionViewModel(
        ILibraryStore libraryStore,
        ILocalEnvironmentDiscoveryService discovery,
        ILocalEnvironmentBaselineStore baselineStore,
        ILocalEnvironmentSnapshotService? snapshotService = null,
        ILocalEnvironmentBaselineService? baselineService = null,
        Func<string, Task<bool>>? confirmActionAsync = null,
        IBeforeUpdateProtectionPolicyStore? policyStore = null,
        IBeforeUpdateProtectionWorkflow? beforeUpdateWorkflow = null,
        IConfigSemanticService? configSemanticService = null)
    {
        _libraryStore = libraryStore;
        _discovery = discovery;
        _baselineStore = baselineStore;
        _snapshotService = snapshotService;
        _baselineService = baselineService;
        _confirmActionAsync = confirmActionAsync ?? (message => Task.FromResult(System.Windows.MessageBox.Show(message, "Environnement local", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.OK));
        _policyStore = policyStore;
        _beforeUpdateWorkflow = beforeUpdateWorkflow;
        _configSemanticService = configSemanticService;
        if (_beforeUpdateWorkflow is not null) _beforeUpdateWorkflow.Changed += BeforeUpdateWorkflow_Changed;
        OpenEntryCommand = new RelayCommand<LocalEnvironmentEntryViewModel>(OpenEntry, entry => entry?.CanOpen == true);
        CreateSnapshotCommand = new AsyncRelayCommand<LocalEnvironmentEntryViewModel>(CreateSnapshotAsync, entry => entry?.CanSnapshot == true && _snapshotService is not null && !IsBusy && !_busyEntryIds.Contains(entry.Id));
        RestoreSnapshotCommand = new AsyncRelayCommand<LocalEnvironmentSnapshotViewModel>(RestoreSnapshotAsync, snapshot => snapshot is not null && _snapshotService is not null && !_busySnapshotIds.Contains(snapshot.Id));
        DeleteSnapshotCommand = new AsyncRelayCommand<LocalEnvironmentSnapshotViewModel>(DeleteSnapshotAsync, snapshot => snapshot is not null && _snapshotService is not null && !_busySnapshotIds.Contains(snapshot.Id));
        SetBaselineCommand = new AsyncRelayCommand(SetBaselineAsync, () => _baselineService is not null && _installationId is not null);
        SetBeforeUpdatePolicyCommand = new AsyncRelayCommand<BeforeUpdateProtectionPolicyOption>(SetBeforeUpdatePolicyAsync, option => option is not null && _policyStore is not null && _installationId is not null);
        AcceptBeforeUpdatePromptCommand = new AsyncRelayCommand(AcceptBeforeUpdatePromptAsync, () => BeforeUpdatePrompt is not null && _beforeUpdateWorkflow is not null);
        DeclineBeforeUpdatePromptCommand = new RelayCommand(DeclineBeforeUpdatePrompt, () => BeforeUpdatePrompt is not null && _beforeUpdateWorkflow is not null);
        ViewConfigChangesCommand = new RelayCommand(() => { IsChangesPanelVisible = true; NotifyProperties(); }, () => ConfigChanges?.HasChanges == true);
        CloseConfigChangesCommand = new RelayCommand(() => { ConfigChanges?.CancelPendingOperations(); IsChangesPanelVisible = false; NotifyProperties(); });
    }

    public bool IsVisible { get; private set; }
    public bool IsBusy { get; private set; }
    public string CustomizationLabel { get; private set; } = string.Empty;
    public string ConfigSummary { get; private set; } = string.Empty;
    public string SaveSummary { get; private set; } = string.Empty;
    public string LogSummary { get; private set; } = string.Empty;
    public string ModSummary { get; private set; } = string.Empty;
    public string BaselineSummary { get; private set; } = string.Empty;
    public string SnapshotSummary { get; private set; } = string.Empty;
    public IReadOnlyList<LocalEnvironmentEntryViewModel> ConfigEntries { get; private set; } = [];
    public IReadOnlyList<LocalEnvironmentEntryViewModel> SaveEntries { get; private set; } = [];
    public IReadOnlyList<LocalEnvironmentEntryViewModel> LogEntries { get; private set; } = [];
    public IReadOnlyList<LocalEnvironmentEntryViewModel> ModEntries { get; private set; } = [];
    public IReadOnlyList<LocalEnvironmentEntryViewModel> EnvironmentEntries { get; private set; } = [];
    public IReadOnlyList<LocalEnvironmentSnapshotViewModel> Snapshots { get; private set; } = [];
    public string SnapshotQuotaLabel { get; private set; } = string.Empty;
    public string SnapshotFeedback { get; private set; } = string.Empty;
    public bool HasSnapshots => Snapshots.Count > 0;
    public bool HasSnapshotQuota => !string.IsNullOrWhiteSpace(SnapshotQuotaLabel);
    public bool HasSnapshotFeedback => !string.IsNullOrWhiteSpace(SnapshotFeedback);
    public BeforeUpdateProtectionPolicy BeforeUpdatePolicy { get; private set; } = BeforeUpdateProtectionPolicy.Ask;
    public IReadOnlyList<BeforeUpdateProtectionPolicyOption> BeforeUpdatePolicyOptions { get; } =
    [
        new(BeforeUpdateProtectionPolicy.Always, "Toujours"),
        new(BeforeUpdateProtectionPolicy.Ask, "Demander"),
        new(BeforeUpdateProtectionPolicy.Never, "Jamais")
    ];
    public bool HasBeforeUpdatePolicy => _policyStore is not null;
    public BeforeUpdateProtectionPrompt? BeforeUpdatePrompt { get; private set; }
    public bool HasBeforeUpdatePrompt => BeforeUpdatePrompt is not null;
    public string BeforeUpdatePromptMessage => BeforeUpdatePrompt is null ? string.Empty : $"Protéger tes réglages avant la mise à jour ?\nPlayStead peut garder une copie des fichiers de configuration importants ({BeforeUpdatePrompt.EligibleConfigurationCount}) avant que le jeu soit mis à jour.";
    public string BeforeUpdateStatusMessage => _beforeUpdateWorkflow?.LastFailureMessage is not null ? "Certains réglages n’ont pas pu être protégés." : _beforeUpdateWorkflow?.LastStatusMessage ?? string.Empty;
    public bool HasBeforeUpdateStatusMessage => !string.IsNullOrWhiteSpace(BeforeUpdateStatusMessage);
    public bool HasConfigSummary => !string.IsNullOrWhiteSpace(ConfigSummary);
    public bool HasSaveSummary => !string.IsNullOrWhiteSpace(SaveSummary);
    public bool HasLogSummary => !string.IsNullOrWhiteSpace(LogSummary);
    public bool HasModSummary => !string.IsNullOrWhiteSpace(ModSummary);
    public bool HasBaselineSummary => !string.IsNullOrWhiteSpace(BaselineSummary);
    public bool HasSnapshotSummary => !string.IsNullOrWhiteSpace(SnapshotSummary);
    public bool HasEmptyState => IsVisible && !EnvironmentEntries.Any() && !HasSnapshotSummary && CustomizationLabel == "État indéterminé";
    public bool HasConfigSemanticChanges => ConfigChanges?.HasChanges == true;
    public ConfigChangesViewModel? ConfigChanges { get; private set; }
    public bool IsChangesPanelVisible { get; private set; }
    public InstallationId? ConfigChangesInstallationId => _installationId;
    public Guid? ConfigChangesEntryId { get; private set; }
    public Guid? ConfigChangesSnapshotId { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand OpenEntryCommand { get; }
    public IAsyncRelayCommand<LocalEnvironmentEntryViewModel> CreateSnapshotCommand { get; }
    public IAsyncRelayCommand<LocalEnvironmentSnapshotViewModel> RestoreSnapshotCommand { get; }
    public IAsyncRelayCommand<LocalEnvironmentSnapshotViewModel> DeleteSnapshotCommand { get; }
    public IAsyncRelayCommand SetBaselineCommand { get; }
    public IAsyncRelayCommand<BeforeUpdateProtectionPolicyOption> SetBeforeUpdatePolicyCommand { get; }
    public IAsyncRelayCommand AcceptBeforeUpdatePromptCommand { get; }
    public IRelayCommand DeclineBeforeUpdatePromptCommand { get; }
    public IRelayCommand ViewConfigChangesCommand { get; }
    public IRelayCommand CloseConfigChangesCommand { get; }

    public async Task LoadAsync(GameId gameId, string installPath, CancellationToken cancellationToken)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loadToken = _loadCts.Token;
        IsBusy = true;
        try
        {
            var snapshot = await _libraryStore.LoadSnapshotAsync(loadToken).ConfigureAwait(false);
            var installation = snapshot.Installations
                .Where(x => x.GameId == gameId && x.IsPresent)
                .OrderByDescending(x => string.Equals(x.InstallPath, installPath, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => x.IsPreferred)
                .FirstOrDefault();
            if (installation is null)
            {
                Clear();
                return;
            }

            _gameId = gameId;
            _installationId = installation.Id;
            BeforeUpdatePolicy = _policyStore is null ? BeforeUpdateProtectionPolicy.Ask : await _policyStore.GetAsync(installation.Id, loadToken);
            await _discovery.RefreshEnvironmentAsync(installation.Id, loadToken).ConfigureAwait(false);
            var environment = await _discovery.GetEnvironmentAsync(installation.Id, loadToken).ConfigureAwait(false);
            var customization = await _discovery.GetCustomizationStateAsync(installation.Id, loadToken).ConfigureAwait(false);
            var baseline = await _baselineStore.GetAsync(installation.Id, loadToken).ConfigureAwait(false);
            var comparisons = baseline is null || _baselineService is null
                ? []
                : await _baselineService.CompareWithBaselineAsync(installation.Id, loadToken).ConfigureAwait(false);
            var entries = environment.Entries;
            ConfigEntries = Project(entries.Where(x => x.Kind == LocalEnvironmentEntryKind.Config));
            SaveEntries = Project(entries.Where(x => x.Kind == LocalEnvironmentEntryKind.Save));
            LogEntries = Project(entries.Where(x => x.Kind == LocalEnvironmentEntryKind.Log));
            ModEntries = Project(entries.Where(x => x.Kind == LocalEnvironmentEntryKind.Mod));
            EnvironmentEntries = ConfigEntries.Concat(SaveEntries).Concat(LogEntries).Concat(ModEntries).ToArray();
            CustomizationLabel = customization switch
            {
                LocalEnvironmentCustomizationState.Customized => "Installation personnalisée",
                LocalEnvironmentCustomizationState.VanillaConfirmed => "Vanilla confirmé",
                _ => "État indéterminé"
            };
            ConfigSummary = Summary("fichier", ConfigEntries);
            SaveSummary = Summary("emplacement", SaveEntries);
            LogSummary = Summary("emplacement", LogEntries);
            ModSummary = ModEntries.Count == 0 ? string.Empty : $"{ModEntries.Count} composant{(ModEntries.Count == 1 ? string.Empty : "s")} détecté{(ModEntries.Count == 1 ? string.Empty : "s")}";
            BaselineSummary = baseline is null ? "PlayStead n’a pas encore d’état de référence pour cette installation." : BuildBaselineSummary(comparisons);
            var snapshotRows = _snapshotService is null ? [] : (await Task.WhenAll(ConfigEntries.Select(x => _snapshotService.GetSnapshotsForEntryAsync(x.Id, loadToken))).ConfigureAwait(false)).SelectMany(x => x).OrderByDescending(x => x.CreatedAtUtc).ToArray();
            Snapshots = snapshotRows.Select(ProjectSnapshot).ToArray();
            await LoadSemanticConfigChangesAsync(snapshotRows, loadToken);
            var snapshotCount = snapshotRows.Length;
            SnapshotSummary = snapshotCount == 0 ? string.Empty : $"{snapshotCount} version{(snapshotCount == 1 ? string.Empty : "s")} conservée{(snapshotCount == 1 ? string.Empty : "s")}";
            SnapshotQuotaLabel = snapshotCount == 0 ? string.Empty : $"Espace utilisé : {FormatBytes(snapshotRows.Sum(x => x.SourceSizeBytes))}";
            SnapshotFeedback = string.Empty;
            RefreshBeforeUpdatePrompt();
            IsVisible = true;
            NotifyProperties();
            NotifyCommands();
        }
        finally { IsBusy = false; NotifyCommands(); }
    }

    private async Task LoadSemanticConfigChangesAsync(IReadOnlyList<LocalFileSnapshot> snapshots, CancellationToken cancellationToken)
    {
        ConfigChanges?.CancelPendingOperations();
        ConfigChanges = null; ConfigChangesEntryId = null; ConfigChangesSnapshotId = null; IsChangesPanelVisible = false;
        if (_configSemanticService is null || _snapshotService is null) return;
        var config = ConfigEntries.FirstOrDefault(x => x.CanSnapshot && File.Exists(x.Path));
        if (config is null) return;
        var candidate = snapshots.Where(x => x.EnvironmentEntryId == config.Id)
            .Where(x => _installationId is not null && x.InstallationId == _installationId.Value)
            .OrderByDescending(x => x.Reason == LocalFileSnapshotReason.BeforeUpdate)
            .ThenByDescending(x => x.Reason == LocalFileSnapshotReason.Manual)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault(x => x.Reason is LocalFileSnapshotReason.BeforeUpdate or LocalFileSnapshotReason.Manual);
        if (candidate is null) return;
        var sourcePath = await _snapshotService.GetSnapshotPathAsync(candidate.Id, cancellationToken);
        if (sourcePath is null) return;
        var changes = new ConfigChangesViewModel(_configSemanticService, _confirmActionAsync);
        await changes.LoadAsync(config.Path, sourcePath, cancellationToken);
        if (!changes.HasChanges) return;
        ConfigChanges = changes; ConfigChangesEntryId = config.Id; ConfigChangesSnapshotId = candidate.Id;
        changes.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(ConfigChanges, changes) && e.PropertyName == nameof(ConfigChangesViewModel.Summary))
            {
                ConfigSummary = Summary("fichier", ConfigEntries) + (changes.HasChanges ? " · " + changes.Summary : string.Empty);
                NotifyProperties(); NotifyCommands();
            }
        };
        ConfigSummary = string.IsNullOrWhiteSpace(ConfigSummary) ? changes.Summary : $"{ConfigSummary} · {changes.Summary}";
        NotifyProperties(); NotifyCommands();
    }

    private IReadOnlyList<LocalEnvironmentEntryViewModel> Project(IEnumerable<LocalEnvironmentEntry> entries) => entries.Select(entry => new LocalEnvironmentEntryViewModel(entry.Id, Path.GetFileName(entry.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), entry.Exists ? "Présent" : "Introuvable", entry.Path, entry.Exists && (File.Exists(entry.Path) || Directory.Exists(entry.Path)), entry.Kind == LocalEnvironmentEntryKind.Config && _snapshotService is not null && SnapshotEligibilityPolicy.Evaluate(entry).IsEligible)).ToArray();

    private static string Summary(string singular, IReadOnlyList<LocalEnvironmentEntryViewModel> entries)
    {
        if (entries.Count == 0) return string.Empty;
        var present = entries.Count(x => x.StatusLabel == "Présent");
        return $"{entries.Count} {singular}{(entries.Count == 1 ? string.Empty : "s")} {((present == entries.Count) ? "présent" : "connu")}{(entries.Count == 1 ? string.Empty : "s")}" + (present == entries.Count ? string.Empty : $" · {entries.Count - present} introuvable{(entries.Count - present == 1 ? string.Empty : "s")}");
    }

    private static string BuildBaselineSummary(IReadOnlyList<LocalEnvironmentComparison> comparisons)
    {
        var changed = comparisons.Count(x => x.State == LocalEnvironmentComparisonState.Changed);
        var added = comparisons.Count(x => x.State == LocalEnvironmentComparisonState.New);
        var missing = comparisons.Count(x => x.State == LocalEnvironmentComparisonState.Missing);
        return changed == 0 && added == 0 && missing == 0 ? "Tout est conforme à ton état de référence" : $"{added + changed + missing} changement{(added + changed + missing == 1 ? string.Empty : "s")} depuis ton état de référence";
    }

    private void OpenEntry(LocalEnvironmentEntryViewModel? entry)
    {
        if (entry is null || !entry.CanOpen) return;
        Process.Start(new ProcessStartInfo { FileName = entry.Path, UseShellExecute = true });
    }

    private async Task CreateSnapshotAsync(LocalEnvironmentEntryViewModel? entry, CancellationToken cancellationToken)
    {
        if (entry is null || _snapshotService is null || _installationId is null) return;
        _busyEntryIds.Add(entry.Id); NotifyCommands();
        try
        {
            await _snapshotService.CreateSnapshotAsync(_installationId.Value, entry.Id, LocalFileSnapshotReason.Manual, cancellationToken);
            SnapshotFeedback = "Réglages protégés.";
            await ReloadSnapshotsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            SnapshotFeedback = $"Protection impossible : {ex.Message}";
            NotifyProperties();
        }
        finally { _busyEntryIds.Remove(entry.Id); NotifyCommands(); }
    }

    private async Task RestoreSnapshotAsync(LocalEnvironmentSnapshotViewModel? snapshot, CancellationToken cancellationToken)
    {
        if (snapshot is null || _snapshotService is null) return;
        if (!await _confirmActionAsync($"Restaurer cette configuration ?\n\nPlayStead gardera automatiquement une copie de l’état actuel avant de revenir à cette version.")) return;
        _busySnapshotIds.Add(snapshot.Id); NotifyCommands();
        try
        {
            await _snapshotService.RestoreSnapshotAsync(snapshot.Id, cancellationToken);
            SnapshotFeedback = "Configuration restaurée. L’état précédent a été conservé.";
            await ReloadSnapshotsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or FileNotFoundException)
        {
            SnapshotFeedback = $"Restauration impossible : {ex.Message}";
            NotifyProperties();
        }
        finally { _busySnapshotIds.Remove(snapshot.Id); NotifyCommands(); }
    }

    private async Task DeleteSnapshotAsync(LocalEnvironmentSnapshotViewModel? snapshot, CancellationToken cancellationToken)
    {
        if (snapshot is null || _snapshotService is null) return;
        if (!await _confirmActionAsync($"Supprimer cette version ?\n\nLe fichier d’origine ne sera pas modifié.")) return;
        _busySnapshotIds.Add(snapshot.Id); NotifyCommands();
        try
        {
            await _snapshotService.DeleteSnapshotAsync(snapshot.Id, cancellationToken);
            SnapshotFeedback = "Version supprimée.";
            await ReloadSnapshotsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or FileNotFoundException)
        {
            SnapshotFeedback = $"Suppression impossible : {ex.Message}";
            NotifyProperties();
        }
        finally { _busySnapshotIds.Remove(snapshot.Id); NotifyCommands(); }
    }

    private async Task ReloadSnapshotsAsync(CancellationToken cancellationToken)
    {
        if (_snapshotService is null || _installationId is null) return;
        var rows = (await _snapshotService.GetSnapshotsAsync(_installationId.Value, cancellationToken)).OrderByDescending(x => x.CreatedAtUtc).ToArray();
        Snapshots = rows.Select(ProjectSnapshot).ToArray();
        SnapshotSummary = rows.Length == 0 ? string.Empty : $"{rows.Length} version{(rows.Length == 1 ? string.Empty : "s")} conservée{(rows.Length == 1 ? string.Empty : "s")}";
        SnapshotQuotaLabel = rows.Length == 0 ? string.Empty : $"Espace utilisé : {FormatBytes(rows.Sum(x => x.SourceSizeBytes))}";
        NotifyProperties();
    }

    private static LocalEnvironmentSnapshotViewModel ProjectSnapshot(LocalFileSnapshot snapshot) => new(snapshot.Id, snapshot.EnvironmentEntryId, Path.GetFileName(snapshot.OriginalPath), snapshot.CreatedAtUtc.ToLocalTime().ToString("g"), snapshot.Reason switch { LocalFileSnapshotReason.BeforeUpdate => "Avant mise à jour", LocalFileSnapshotReason.PreRestore => "État avant restauration", _ => "Créée manuellement" }, FormatBytes(snapshot.SourceSizeBytes), snapshot.IsPreRestoreBackup);
    private static string FormatBytes(long bytes) => bytes < 1024 ? $"{bytes} o" : bytes < 1024 * 1024 ? $"{bytes / 1024d:0.#} Ko" : $"{bytes / (1024d * 1024d):0.#} Mo";

    private async Task SetBaselineAsync()
    {
        if (_baselineService is not null && _installationId is not null)
            await _baselineService.SetBaselineAsync(_installationId.Value, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task SetBeforeUpdatePolicyAsync(BeforeUpdateProtectionPolicyOption? option)
    {
        if (option is null || _policyStore is null || _installationId is null) return;
        await _policyStore.SetAsync(_installationId.Value, option.Value, CancellationToken.None);
        BeforeUpdatePolicy = option.Value;
        NotifyProperties();
    }

    private void Clear()
    {
        _loadCts?.Cancel();
        ConfigChanges?.CancelPendingOperations();
        _gameId = null; _installationId = null; IsVisible = false; BeforeUpdatePolicy = BeforeUpdateProtectionPolicy.Ask; BeforeUpdatePrompt = null; ConfigChanges = null; ConfigChangesEntryId = null; ConfigChangesSnapshotId = null; IsChangesPanelVisible = false; ConfigEntries = []; SaveEntries = []; LogEntries = []; ModEntries = []; EnvironmentEntries = []; Snapshots = []; ConfigSummary = SaveSummary = LogSummary = ModSummary = SnapshotSummary = SnapshotQuotaLabel = SnapshotFeedback = string.Empty; BaselineSummary = string.Empty; CustomizationLabel = string.Empty; NotifyProperties(); NotifyCommands();
    }

    public void CancelPendingOperations()
    {
        _loadCts?.Cancel();
        ConfigChanges?.CancelPendingOperations();
    }

    private void NotifyProperties()
    {
        foreach (var name in new[] { nameof(IsVisible), nameof(IsBusy), nameof(CustomizationLabel), nameof(ConfigSummary), nameof(SaveSummary), nameof(LogSummary), nameof(ModSummary), nameof(BaselineSummary), nameof(SnapshotSummary), nameof(SnapshotQuotaLabel), nameof(SnapshotFeedback), nameof(BeforeUpdatePolicy), nameof(BeforeUpdatePolicyOptions), nameof(HasBeforeUpdatePolicy), nameof(BeforeUpdatePrompt), nameof(HasBeforeUpdatePrompt), nameof(BeforeUpdatePromptMessage), nameof(BeforeUpdateStatusMessage), nameof(HasBeforeUpdateStatusMessage), nameof(ConfigEntries), nameof(SaveEntries), nameof(LogEntries), nameof(ModEntries), nameof(EnvironmentEntries), nameof(Snapshots), nameof(HasConfigSummary), nameof(HasSaveSummary), nameof(HasLogSummary), nameof(HasModSummary), nameof(HasBaselineSummary), nameof(HasSnapshotSummary), nameof(HasSnapshots), nameof(HasSnapshotQuota), nameof(HasSnapshotFeedback), nameof(HasEmptyState), nameof(ConfigChanges), nameof(HasConfigSemanticChanges), nameof(IsChangesPanelVisible), nameof(ConfigChangesInstallationId), nameof(ConfigChangesEntryId), nameof(ConfigChangesSnapshotId) })
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void NotifyCommands()
    {
        CreateSnapshotCommand.NotifyCanExecuteChanged(); RestoreSnapshotCommand.NotifyCanExecuteChanged(); DeleteSnapshotCommand.NotifyCanExecuteChanged(); SetBaselineCommand.NotifyCanExecuteChanged(); SetBeforeUpdatePolicyCommand.NotifyCanExecuteChanged(); AcceptBeforeUpdatePromptCommand.NotifyCanExecuteChanged(); DeclineBeforeUpdatePromptCommand.NotifyCanExecuteChanged(); ViewConfigChangesCommand.NotifyCanExecuteChanged();
    }

    private void BeforeUpdateWorkflow_Changed(object? sender, EventArgs e)
    {
        void Refresh() { RefreshBeforeUpdatePrompt(); NotifyProperties(); NotifyCommands(); }
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Refresh(); else _ = dispatcher.BeginInvoke(Refresh);
    }

    private void RefreshBeforeUpdatePrompt() => BeforeUpdatePrompt = _beforeUpdateWorkflow?.PendingPrompts.FirstOrDefault(x => x.InstallationId == _installationId && x.GameId == _gameId?.Value);

    private async Task AcceptBeforeUpdatePromptAsync(CancellationToken cancellationToken)
    {
        if (_beforeUpdateWorkflow is null || BeforeUpdatePrompt is null) return;
        await _beforeUpdateWorkflow.AcceptAsync(BeforeUpdatePrompt.Id, cancellationToken); RefreshBeforeUpdatePrompt(); NotifyProperties(); NotifyCommands();
    }

    private void DeclineBeforeUpdatePrompt()
    {
        if (_beforeUpdateWorkflow is null || BeforeUpdatePrompt is null) return;
        _beforeUpdateWorkflow.Decline(BeforeUpdatePrompt.Id); RefreshBeforeUpdatePrompt(); NotifyProperties(); NotifyCommands();
    }
}
