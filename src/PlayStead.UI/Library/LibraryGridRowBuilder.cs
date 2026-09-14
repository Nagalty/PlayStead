namespace PlayStead.UI.Library;

public static class LibraryGridRowBuilder
{
    public static IReadOnlyList<LibraryGridRow> Build(
        IReadOnlyList<LibraryItemViewModel> items,
        int columnCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (columnCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        if (items.Count == 0)
        {
            return Array.Empty<LibraryGridRow>();
        }

        var rows = new List<LibraryGridRow>();
        var offset = 0;

        while (offset < items.Count)
        {
            var rowSize = Math.Min(columnCount, items.Count - offset);
            var rowItems = new LibraryItemViewModel[rowSize];

            for (var index = 0; index < rowSize; index++)
            {
                rowItems[index] = items[offset + index];
            }

            rows.Add(new LibraryGridRow(rowItems));
            offset += rowSize;
        }

        return rows;
    }
}
