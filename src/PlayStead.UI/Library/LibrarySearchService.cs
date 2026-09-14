namespace PlayStead.UI.Library;

public static class LibrarySearchService
{
    public static IReadOnlyList<LibraryItemViewModel> Search(
        IEnumerable<LibraryItemViewModel> items,
        string query)
    {
        ArgumentNullException.ThrowIfNull(
            items);
        ArgumentNullException.ThrowIfNull(
            query);

        var normalizedQuery =
            query.Trim();

        if (normalizedQuery.Length == 0)
        {
            return items as IReadOnlyList<LibraryItemViewModel>
                ?? items.ToArray();
        }

        return items
            .Where(
                item =>
                    item.Title.Contains(
                        normalizedQuery,
                        StringComparison.CurrentCultureIgnoreCase) ||
                    item.ProviderLabel.Contains(
                        normalizedQuery,
                        StringComparison.CurrentCultureIgnoreCase) ||
                    item.InstallPath.Contains(
                        normalizedQuery,
                        StringComparison.CurrentCultureIgnoreCase))
            .ToArray();
    }
}
