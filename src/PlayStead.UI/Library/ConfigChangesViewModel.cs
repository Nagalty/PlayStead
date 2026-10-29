using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using PlayStead.Core.Configuration;

namespace PlayStead.UI.Library;

public sealed class ConfigChangeRowViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    public ConfigChangeRowViewModel(ConfigSemanticChange change)
    {
        Change = change;
        DisplayName = string.IsNullOrWhiteSpace(change.DisplayName) ? change.Path : change.DisplayName;
        KindLabel = change.ChangeKind switch { ConfigChangeKind.Added => "Ajouté", ConfigChangeKind.Removed => "Supprimé", _ => "Modifié" };
        ValuesLabel = $"{change.OldValue ?? "—"} → {change.CurrentValue ?? "—"}";
        RestoreStatusLabel = change.CanRestore ? string.Empty : "Ce réglage ne peut pas être restauré automatiquement.";
    }
    public ConfigSemanticChange Change { get; }
    public string DisplayName { get; }
    public string KindLabel { get; }
    public string ValuesLabel { get; }
    public string RestoreStatusLabel { get; }
    public bool HasRestoreStatus => !string.IsNullOrWhiteSpace(RestoreStatusLabel);
    public bool CanSelect => Change.CanRestore;
    public bool IsSelected { get => _isSelected; set { if (!CanSelect || _isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); SelectionChanged?.Invoke(this, EventArgs.Empty); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SelectionChanged;
}

public sealed class ConfigChangesViewModel : INotifyPropertyChanged
{
    private readonly IConfigSemanticService _service;
    private readonly Func<string, Task<bool>> _confirm;
    private string _currentPath = string.Empty;
    private string _referencePath = string.Empty;
    private bool _isLoadingChanges;
    private bool _isRestoringSelectedChanges;
    private CancellationTokenSource? _operationCts;
    private int _generation;
    public ConfigChangesViewModel(IConfigSemanticService service, Func<string, Task<bool>>? confirm = null)
    {
        _service = service;
        _confirm = confirm ?? (message => Task.FromResult(System.Windows.MessageBox.Show(message, "Retrouver ces réglages ?", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.OK));
        RestoreSelectedChangesCommand = new AsyncRelayCommand(RestoreSelectedChangesAsync, () => CanRestoreSelectedChanges);
    }
    public ObservableCollection<ConfigChangeRowViewModel> Changes { get; } = [];
    public bool IsLoadingChanges { get => _isLoadingChanges; private set { _isLoadingChanges = value; OnChanged(); } }
    public bool IsRestoringSelectedChanges { get => _isRestoringSelectedChanges; private set { _isRestoringSelectedChanges = value; OnChanged(); OnChanged(nameof(CanRestoreSelectedChanges)); RestoreSelectedChangesCommand.NotifyCanExecuteChanged(); } }
    public bool CanRestoreSelectedChanges => !IsRestoringSelectedChanges && Changes.Any(x => x.IsSelected);
    public string Summary => Changes.Count == 0 ? string.Empty : Changes.Count == 1 ? "1 réglage a changé" : $"{Changes.Count} réglages ont changé";
    public string Feedback { get; private set; } = string.Empty;
    public bool HasChanges => Changes.Count > 0;
    public IAsyncRelayCommand RestoreSelectedChangesCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadAsync(string currentPath, string referencePath, CancellationToken cancellationToken)
    {
        var generation = ++_generation;
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var operationToken = _operationCts.Token;
        _currentPath = currentPath; _referencePath = referencePath; IsLoadingChanges = true; Feedback = string.Empty;
        try
        {
            var changes = await _service.CompareConfigAsync(currentPath, referencePath, operationToken);
            operationToken.ThrowIfCancellationRequested();
            Changes.Clear(); foreach (var change in changes) { var row = new ConfigChangeRowViewModel(change); row.SelectionChanged += (_, _) => { OnChanged(nameof(CanRestoreSelectedChanges)); RestoreSelectedChangesCommand.NotifyCanExecuteChanged(); }; Changes.Add(row); }
            OnChanged(nameof(Summary)); OnChanged(nameof(HasChanges)); OnChanged(nameof(CanRestoreSelectedChanges));
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested) { }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or FormatException or NotSupportedException or System.Text.Json.JsonException)
        {
            if (generation != _generation || operationToken.IsCancellationRequested) return;
            Changes.Clear(); Feedback = "Comparaison indisponible. Vérifie que les fichiers sont accessibles et valides."; OnChanged(nameof(Feedback)); OnChanged(nameof(HasChanges));
        }
        finally { if (generation == _generation) IsLoadingChanges = false; }
    }

    public void CancelPendingOperations() { ++_generation; _operationCts?.Cancel(); IsLoadingChanges = false; }

    private async Task RestoreSelectedChangesAsync()
    {
        var selected = Changes.Where(x => x.IsSelected).Select(x => x.Change.Path).ToArray();
        var generation = _generation;
        var currentPath = _currentPath;
        var referencePath = _referencePath;
        var token = _operationCts?.Token ?? CancellationToken.None;
        if (selected.Length == 0 || !await _confirm("Retrouver ces réglages ?\n\nPlayStead modifiera uniquement les réglages sélectionnés. Les autres valeurs actuelles seront conservées.")) return;
        if (generation != _generation || token.IsCancellationRequested) return;
        IsRestoringSelectedChanges = true;
        try
        {
            var result = await _service.RestoreConfigValuesAsync(currentPath, referencePath, selected, token);
            token.ThrowIfCancellationRequested();
            Feedback = result.Succeeded ? $"{result.AppliedChanges.Count} réglage{(result.AppliedChanges.Count == 1 ? string.Empty : "s")} restauré{(result.AppliedChanges.Count == 1 ? string.Empty : "s")}. Une copie de l’état précédent a été conservée." : result.FailureReason ?? "Restauration impossible.";
            OnChanged(nameof(Feedback));
            if (result.Succeeded)
            {
                var feedback = Feedback;
                await LoadAsync(currentPath, referencePath, CancellationToken.None);
                if (_currentPath == currentPath && !(_operationCts?.IsCancellationRequested ?? true)) { Feedback = feedback; OnChanged(nameof(Feedback)); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { IsRestoringSelectedChanges = false; }
    }

    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
