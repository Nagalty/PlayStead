using PlayStead.Core.Library;

namespace PlayStead.UI.Library;

public sealed record LibraryUiState(
    LibraryViewMode ViewMode,
    string SortKey,
    string? FilterKey,
    GameId? SelectedGameId,
    double VerticalOffset)
{
    public LibraryQuickFilter QuickFilter { get; init; } = LibraryQuickFilter.Installed;

    public IReadOnlyList<string> ProviderFilters { get; init; } = [];

    public IReadOnlyList<string> DriveFilters { get; init; } = [];

    public IReadOnlyList<string> SelectedCapabilities { get; init; } = [];

    public IReadOnlyList<string> SelectedGenres { get; init; } = [];

    public IReadOnlyList<Guid> CollectionFilters { get; init; } = [];

    public string SearchText { get; init; } = string.Empty;
}
