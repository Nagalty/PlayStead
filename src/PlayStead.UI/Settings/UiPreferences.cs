using PlayStead.UI.Library;

namespace PlayStead.UI.Settings;

public sealed record UiPreferences(
    bool ReduceMotion = false,
    LibraryViewMode LibraryViewMode = LibraryViewMode.Grid,
    string LibrarySortKey = "Title",
    string? LibraryFilterKey = null,
    Guid? LastDormantGameId = null);
