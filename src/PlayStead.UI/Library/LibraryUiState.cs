using PlayStead.Core.Library;

namespace PlayStead.UI.Library;

public sealed record LibraryUiState(
    LibraryViewMode ViewMode,
    string SortKey,
    string? FilterKey,
    GameId? SelectedGameId,
    double VerticalOffset);
