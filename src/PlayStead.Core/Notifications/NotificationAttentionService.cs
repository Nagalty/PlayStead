using System.Security.Cryptography;
using System.Text;
using PlayStead.Core.Persistence;
using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.Core.Notifications;

public sealed class NotificationAttentionService : IAttentionService
{
    private readonly INotificationCenterService _notifications;
    private readonly ProviderInstallUpdateStateReconciliationService? _installUpdates;
    private readonly ILibraryStore? _libraryStore;
    private IReadOnlyList<AttentionItem> _items = [];

    public NotificationAttentionService(
        INotificationCenterService notifications,
        ProviderInstallUpdateStateReconciliationService? installUpdates = null,
        ILibraryStore? libraryStore = null)
    {
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _installUpdates = installUpdates;
        _libraryStore = libraryStore;
        if (_installUpdates is not null)
        {
            _installUpdates.Changed += InstallUpdatesOnChanged;
        }
    }

    public IReadOnlyList<AttentionItem> Items => _items;
    public event EventHandler? Changed;

    public async Task<IReadOnlyList<AttentionItem>> GetActiveAsync(CancellationToken cancellationToken)
    {
        var records = await _notifications.ListAsync(NotificationListFilter.Active, cancellationToken);
        var items = records.Where(x => x.Priority == NotificationPriority.ActionRequired)
            .OrderByDescending(x => x.UpdatedUtc)
            .ThenBy(x => x.NotificationId.Value)
            .Select(x => new AttentionItem(x.NotificationId,
                Guid.TryParse(x.SubjectId, out var gameId) ? gameId : null,
                x.Title, x.Message, AttentionSeverity.ActionRequired, x.UpdatedUtc))
            .ToList();

        if (_installUpdates is null)
        {
            return items;
        }

        var states = _installUpdates.GetAll()
            .Where(x => x.Status is ProviderInstallUpdateStatus.UpdateAvailable
                or ProviderInstallUpdateStatus.Downloading
                or ProviderInstallUpdateStatus.Staging)
            .ToArray();
        if (states.Length == 0)
        {
            return items;
        }

        var titles = _libraryStore is null
            ? new Dictionary<Guid, string>()
            : (await _libraryStore.LoadSnapshotAsync(cancellationToken)).Games
                .ToDictionary(x => x.Id.Value, x => x.Title);

        foreach (var state in states)
        {
            var gameTitle = titles.TryGetValue(state.GameId.Value, out var title)
                ? title
                : state.ProviderGameId;
            var message = state.Status switch
            {
                ProviderInstallUpdateStatus.Downloading => "La mise à jour Steam est en cours de téléchargement.",
                ProviderInstallUpdateStatus.Staging => "La mise à jour Steam est en préparation.",
                _ => "Une mise à jour Steam est prête à être téléchargée."
            };
            items.Add(new AttentionItem(
                CreateInstallUpdateAttentionId(state),
                state.GameId.Value,
                $"{gameTitle} — mise à jour disponible",
                message,
                AttentionSeverity.ActionRequired,
                DateTimeOffset.UnixEpoch));
        }

        return items;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var next = await GetActiveAsync(cancellationToken);
        if (_items.SequenceEqual(next)) return;
        _items = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void InstallUpdatesOnChanged(object? sender, EventArgs e) => _ = RefreshSafelyAsync();

    private async Task RefreshSafelyAsync()
    {
        try
        {
            await RefreshAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static NotificationId CreateInstallUpdateAttentionId(ProviderInstallUpdateState state)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"provider-install-update:{state.Provider}:{state.GameId.Value:D}"));
        return new NotificationId(new Guid(bytes.AsSpan(0, 16)));
    }
}
