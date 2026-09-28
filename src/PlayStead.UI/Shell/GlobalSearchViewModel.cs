using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using PlayStead.Core.Library;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;

namespace PlayStead.UI.Shell;

public sealed record GlobalSearchResult(
    GameId GameId,
    string Title,
    string ProviderLabel,
    string? CoverPath);

public sealed class GlobalSearchViewModel : INotifyPropertyChanged
{
    private readonly NavigationService _navigation;
    private IReadOnlyList<LibraryItemViewModel> _items = [];
    private IReadOnlyDictionary<GameId, IReadOnlyList<string>> _collectionNamesByGame =
        new Dictionary<GameId, IReadOnlyList<string>>();
    private IReadOnlyList<GlobalSearchResult> _results = [];
    private string _query = string.Empty;

    public GlobalSearchViewModel(NavigationService navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        SelectResultCommand = new RelayCommand<GlobalSearchResult?>(SelectResult);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Query => _query;

    public IReadOnlyList<GlobalSearchResult> Results => _results;

    public bool IsOpen => !string.IsNullOrWhiteSpace(_query);

    public RelayCommand<GlobalSearchResult?> SelectResultCommand { get; }

    public void SetItems(IEnumerable<LibraryItemViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();
        RebuildResults();
    }

    public void SetCollectionNames(IReadOnlyDictionary<GameId, IReadOnlyList<string>> collectionNamesByGame)
    {
        ArgumentNullException.ThrowIfNull(collectionNamesByGame);
        _collectionNamesByGame = collectionNamesByGame;
        RebuildResults();
    }

    public void SetQuery(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.Equals(_query, query, StringComparison.Ordinal))
            return;

        _query = query;
        OnPropertyChanged(nameof(Query));
        RebuildResults();
    }

    public void Close() => SetQuery(string.Empty);

    private void RebuildResults()
    {
        var normalizedQuery = Normalize(_query);
        if (normalizedQuery.Length == 0)
        {
            _results = [];
        }
        else
        {
            _results = _items
                .GroupBy(item => item.GameId)
                .Select(group => group.First())
                .Select(item =>
                {
                    var title = Normalize(item.Title);
                    var collections = _collectionNamesByGame.TryGetValue(item.GameId, out var names)
                        ? names.Select(Normalize).ToArray()
                        : [];
                    var titleStarts = title.StartsWith(normalizedQuery, StringComparison.Ordinal);
                    var titleContains = title.Contains(normalizedQuery, StringComparison.Ordinal);
                    var collectionStarts = collections.Any(name => name.StartsWith(normalizedQuery, StringComparison.Ordinal));
                    var collectionContains = collections.Any(name => name.Contains(normalizedQuery, StringComparison.Ordinal));
                    var rank = titleStarts ? 0 : titleContains ? 1 : collectionStarts ? 2 : collectionContains ? 3 : 4;
                    return (Item: item, Title: title, Rank: rank, Matches: titleContains || collectionContains);
                })
                .Where(candidate => candidate.Matches)
                .OrderBy(candidate => candidate.Rank)
                .ThenBy(candidate => candidate.Title, StringComparer.CurrentCultureIgnoreCase)
                .Take(8)
                .Select(candidate => new GlobalSearchResult(
                    candidate.Item.GameId,
                    candidate.Item.Title,
                    candidate.Item.ProviderLabel,
                    candidate.Item.CoverPath))
                .ToArray();
        }

        OnPropertyChanged(nameof(Results));
        OnPropertyChanged(nameof(IsOpen));
    }

    private void SelectResult(GlobalSearchResult? result)
    {
        if (result is null)
            return;

        _navigation.Navigate(new NavigationRequest(AppRoute.GameDetail, result.GameId));
        Close();
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
