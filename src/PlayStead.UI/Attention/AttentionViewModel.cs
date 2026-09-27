using System.ComponentModel;
using PlayStead.Core.Notifications;

namespace PlayStead.UI.Attention;

public sealed class AttentionViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IAttentionService? _service;
    public AttentionViewModel() { }
    public AttentionViewModel(IAttentionService service) { _service = service; service.Changed += ServiceOnChanged; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public string EmptyMessage => "Aucune décision ni vérification à signaler pour le moment.";
    public IReadOnlyList<AttentionItem> Items => _service?.Items ?? [];
    public bool HasItems => Items.Count > 0;
    public async Task RefreshAsync(CancellationToken cancellationToken) { if (_service is not null) { await _service.RefreshAsync(cancellationToken); Notify(); } }
    private void ServiceOnChanged(object? sender, EventArgs e) => Notify();
    private void Notify() { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasItems))); }
    public void Dispose() { if (_service is not null) _service.Changed -= ServiceOnChanged; }
}
