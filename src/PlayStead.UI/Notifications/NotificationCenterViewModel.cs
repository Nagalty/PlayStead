using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using PlayStead.Core.Notifications;

namespace PlayStead.UI.Notifications;

public sealed class NotificationCenterViewModel : INotifyPropertyChanged
{
    private readonly INotificationCenterService _service;
    private IReadOnlyList<NotificationRecord> _items = [];
    private NotificationListFilter _filter = NotificationListFilter.Active;
    private int _activeCount;
    private bool _isPanelOpen;

    public NotificationCenterViewModel(INotificationCenterService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
        TogglePanelCommand = new AsyncRelayCommand(TogglePanelAsync);
        SelectFilterCommand = new AsyncRelayCommand<NotificationListFilter>(SelectFilterAsync);
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
        OnPropertyChanged(nameof(SelectedItem));
    }

    private async Task TogglePanelAsync()
    {
        IsPanelOpen = !IsPanelOpen;
        if (IsPanelOpen) await RefreshAsync(CancellationToken.None);
    }

    private async Task SelectFilterAsync(NotificationListFilter filter)
    {
        Filter = filter;
        await RefreshAsync(CancellationToken.None);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
