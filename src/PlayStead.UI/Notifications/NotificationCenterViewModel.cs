using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Notifications;

namespace PlayStead.UI.Notifications;

public sealed class NotificationCenterViewModel : INotifyPropertyChanged
{
    private readonly INotificationCenterService _service;
    private readonly IIdentityDecisionApplicationService? _decisionApplication;
    private IReadOnlyList<NotificationRecord> _items = [];
    private NotificationListFilter _filter = NotificationListFilter.Active;
    private int _activeCount;
    private bool _isPanelOpen;
    private bool _isDecisionActionInProgress;

    public NotificationCenterViewModel(INotificationCenterService service)
        : this(service, null)
    {
    }

    public NotificationCenterViewModel(INotificationCenterService service, IIdentityDecisionApplicationService? decisionApplication)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
        _decisionApplication = decisionApplication;
        TogglePanelCommand = new AsyncRelayCommand(TogglePanelAsync);
        SelectFilterCommand = new AsyncRelayCommand<object?>(SelectFilterAsync);
        SelectNotificationCommand = new AsyncRelayCommand<object?>(SelectNotificationParameterAsync);
        ConfirmDecisionCommand = new AsyncRelayCommand(ConfirmAsync, () => !IsDecisionActionInProgress);
        RejectDecisionCommand = new AsyncRelayCommand<object?>(RejectParameterAsync, _ => !IsDecisionActionInProgress);
        RejectSingleCandidateCommand = new AsyncRelayCommand(RejectSingleAsync, () => !IsDecisionActionInProgress);
        ChooseDecisionCommand = new AsyncRelayCommand<object?>(ChooseParameterAsync, _ => !IsDecisionActionInProgress);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<NotificationRecord> Items => _items;
    public NotificationRecord? SelectedItem { get; private set; }
    public NotificationListFilter Filter { get => _filter; private set => SetField(ref _filter, value); }
    public int ActiveCount
    {
        get => _activeCount;
        private set
        {
            if (!SetField(ref _activeCount, value)) return;
            OnPropertyChanged(nameof(IsBadgeVisible));
        }
    }
    public bool IsBadgeVisible => ActiveCount > 0;
    public bool IsPanelOpen { get => _isPanelOpen; private set => SetField(ref _isPanelOpen, value); }
    public ICommand TogglePanelCommand { get; }
    public ICommand SelectFilterCommand { get; }
    public IAsyncRelayCommand<object?> SelectNotificationCommand { get; }
    public IAsyncRelayCommand ConfirmDecisionCommand { get; }
    public IAsyncRelayCommand<object?> RejectDecisionCommand { get; }
    public IAsyncRelayCommand RejectSingleCandidateCommand { get; }
    public IAsyncRelayCommand<object?> ChooseDecisionCommand { get; }
    public IReadOnlyList<CatalogContentId> DecisionCandidates { get; private set; } = [];
    public bool IsDecisionActionVisible => _decisionApplication is not null && SelectedItem is { Producer: NotificationProducer.IdentityResolution, Priority: NotificationPriority.ActionRequired } && DecisionContext is not null && (DecisionContext.State is IdentityResolutionState.MatchProbable or IdentityResolutionState.Ambiguous);
    public bool IsSingleCandidateDecisionVisible => IsDecisionActionVisible && DecisionContext!.State == IdentityResolutionState.MatchProbable;
    public bool IsAmbiguousDecisionVisible => IsDecisionActionVisible && DecisionContext!.State == IdentityResolutionState.Ambiguous;
    public bool IsDecisionActionInProgress => _isDecisionActionInProgress;
    public IdentityDecisionContext? DecisionContext { get; private set; }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var count = await _service.GetActiveCountAsync(cancellationToken);
        var items = await _service.ListAsync(Filter, cancellationToken);
        ActiveCount = count;
        _items = items;
        OnPropertyChanged(nameof(Items));
    }

    public async Task SelectAsync(NotificationId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var item = _items.FirstOrDefault(x => x.NotificationId == id);
        if (item is null) return;
        SelectedItem = item;
        if (item.State == NotificationState.Unread)
            SelectedItem = await _service.MarkReadAsync(id, cancellationToken);
        await RefreshAsync(cancellationToken);
        await LoadDecisionContextAsync(cancellationToken);
        OnPropertyChanged(nameof(SelectedItem));
    }

    public async Task LoadDecisionContextAsync(CancellationToken cancellationToken)
    {
        DecisionContext = null;
        DecisionCandidates = [];
        var selected = SelectedItem;
        if (_decisionApplication is null || selected is null || selected.Producer != NotificationProducer.IdentityResolution || selected.Priority != NotificationPriority.ActionRequired || !Guid.TryParse(selected.SubjectId, out var gameValue))
        {
            OnPropertyChanged(nameof(DecisionContext));
            OnPropertyChanged(nameof(DecisionCandidates));
            OnPropertyChanged(nameof(IsDecisionActionVisible));
            OnPropertyChanged(nameof(IsSingleCandidateDecisionVisible)); OnPropertyChanged(nameof(IsAmbiguousDecisionVisible));
            return;
        }
        var selectedGameId = new GameId(gameValue);
        DecisionContext = await _decisionApplication.GetContextAsync(selectedGameId, cancellationToken);
        if (DecisionContext is not null && DecisionContext.GameId != selectedGameId)
            DecisionContext = null;
        if (DecisionContext is { State: IdentityResolutionState.MatchProbable or IdentityResolutionState.Ambiguous })
            DecisionCandidates = DecisionContext.Candidates.Select(x => x.CatalogContentId).ToArray();
        OnPropertyChanged(nameof(DecisionContext));
        OnPropertyChanged(nameof(DecisionCandidates));
        OnPropertyChanged(nameof(IsDecisionActionVisible));
        OnPropertyChanged(nameof(IsSingleCandidateDecisionVisible)); OnPropertyChanged(nameof(IsAmbiguousDecisionVisible));
    }

    public async Task ConfirmAsync(CancellationToken cancellationToken)
    {
        if (!IsDecisionActionVisible || DecisionContext is null || DecisionContext.Candidates.Count != 1) return;
        await RunDecisionAsync(() => _decisionApplication!.ConfirmAsync(DecisionContext.GameId, DecisionContext.Candidates[0].CatalogContentId, cancellationToken), cancellationToken);
    }

    public async Task ConfirmAsync(CatalogContentId catalogContentId, CancellationToken cancellationToken)
    {
        if (!IsDecisionActionVisible || DecisionContext is null || !DecisionContext.Candidates.Any(x => x.CatalogContentId == catalogContentId)) return;
        await RunDecisionAsync(() => _decisionApplication!.ConfirmAsync(DecisionContext.GameId, catalogContentId, cancellationToken), cancellationToken);
    }

    public async Task RejectAsync(CatalogContentId catalogContentId, CancellationToken cancellationToken)
    {
        if (!IsDecisionActionVisible || DecisionContext is null || !DecisionContext.Candidates.Any(x => x.CatalogContentId == catalogContentId)) return;
        await RunDecisionAsync(() => _decisionApplication!.RejectAsync(DecisionContext.GameId, catalogContentId, cancellationToken), cancellationToken);
    }

    private Task RejectParameterAsync(object? parameter) => parameter is CatalogContentId id ? RejectAsync(id, CancellationToken.None) : Task.CompletedTask;
    private Task RejectSingleAsync() => DecisionContext?.Candidates.Count == 1 ? RejectAsync(DecisionContext.Candidates[0].CatalogContentId, CancellationToken.None) : Task.CompletedTask;
    private Task ChooseParameterAsync(object? parameter) => parameter is CatalogContentId id ? ConfirmAsync(id, CancellationToken.None) : Task.CompletedTask;
    private async Task RunDecisionAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        if (IsDecisionActionInProgress) return;
        _isDecisionActionInProgress = true; OnPropertyChanged(nameof(IsDecisionActionInProgress));
        NotifyDecisionCommandsCanExecuteChanged();
        try { await action(); await RefreshAsync(cancellationToken); await LoadDecisionContextAsync(cancellationToken); }
        finally { _isDecisionActionInProgress = false; OnPropertyChanged(nameof(IsDecisionActionInProgress)); NotifyDecisionCommandsCanExecuteChanged(); }
    }

    private void NotifyDecisionCommandsCanExecuteChanged()
    {
        ConfirmDecisionCommand.NotifyCanExecuteChanged();
        RejectDecisionCommand.NotifyCanExecuteChanged();
        RejectSingleCandidateCommand.NotifyCanExecuteChanged();
        ChooseDecisionCommand.NotifyCanExecuteChanged();
    }

    private async Task TogglePanelAsync()
    {
        IsPanelOpen = !IsPanelOpen;
        if (IsPanelOpen) await RefreshAsync(CancellationToken.None);
    }

    private async Task SelectFilterAsync(object? parameter)
    {
        if (parameter is not NotificationListFilter filter &&
            !(parameter is string text && Enum.TryParse(text, ignoreCase: true, out filter)))
            return;
        Filter = filter;
        await RefreshAsync(CancellationToken.None);
    }

    private Task SelectNotificationParameterAsync(object? parameter) => parameter is NotificationId id ? SelectAsync(id, CancellationToken.None) : Task.CompletedTask;

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
