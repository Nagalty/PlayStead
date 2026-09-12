using System.ComponentModel;
using System.Runtime.CompilerServices;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.UI.Library;

public sealed class LibraryViewModel : INotifyPropertyChanged
{
    private readonly ILibraryStore _libraryStore;

    private IReadOnlyList<LibraryItemViewModel> _items =
        Array.Empty<LibraryItemViewModel>();

    public LibraryViewModel(ILibraryStore libraryStore)
    {
        _libraryStore = libraryStore;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<LibraryItemViewModel> Items
    {
        get => _items;
        private set
        {
            if (ReferenceEquals(_items, value))
            {
                return;
            }

            _items = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasItems));
        }
    }

    public bool HasItems => Items.Count > 0;

    public async Task RefreshAsync(
        CancellationToken cancellationToken)
    {
        var snapshot = await _libraryStore.LoadSnapshotAsync(
            cancellationToken);

        var presentInstallations = snapshot.Installations
            .Where(x => x.IsPresent)
            .GroupBy(x => x.GameId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.IsPreferred)
                    .ThenBy(x => x.Provider)
                    .ThenBy(
                        x => x.InstallPath,
                        StringComparer.OrdinalIgnoreCase)
                    .First());

        Items = snapshot.Games
            .Where(game => presentInstallations.ContainsKey(game.Id))
            .Select(game =>
            {
                var installation = presentInstallations[game.Id];

                return new LibraryItemViewModel(
                    game.Id,
                    game.Title,
                    installation.Provider,
                    ProviderLabel(installation.Provider),
                    installation.InstallPath,
                    installation.InstalledSizeBytes);
            })
            .OrderBy(
                item => item.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string ProviderLabel(ProviderKind provider) =>
        provider switch
        {
            ProviderKind.Steam => "Steam",
            ProviderKind.Epic => "Epic",
            ProviderKind.Gog => "GOG",
            ProviderKind.Manual => "Manual",
            _ => provider.ToString()
        };

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
}
