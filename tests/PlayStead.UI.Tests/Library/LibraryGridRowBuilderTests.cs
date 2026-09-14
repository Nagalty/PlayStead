using PlayStead.Core.Library;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryGridRowBuilderTests
{
    [Fact]
    public void Build_returns_no_rows_for_empty_items()
    {
        var result =
            LibraryGridRowBuilder.Build(
                Array.Empty<LibraryItemViewModel>(),
                columnCount: 3);

        Assert.Empty(
            result);
    }

    [Fact]
    public void Build_places_single_item_in_single_row()
    {
        var item =
            CreateItem(
                "Alpha");

        var result =
            LibraryGridRowBuilder.Build(
                [item],
                columnCount: 3);

        var row =
            Assert.Single(
                result);

        var actual =
            Assert.Single(
                row.Items);

        Assert.Same(
            item,
            actual);
    }

    [Fact]
    public void Build_creates_exact_rows_when_item_count_is_multiple_of_column_count()
    {
        var items =
            new[]
            {
                CreateItem("Alpha"),
                CreateItem("Bravo"),
                CreateItem("Charlie"),
                CreateItem("Delta")
            };

        var result =
            LibraryGridRowBuilder.Build(
                items,
                columnCount: 2);

        Assert.Equal(
            2,
            result.Count);

        Assert.Equal(
            items[..2],
            result[0].Items);

        Assert.Equal(
            items[2..],
            result[1].Items);
    }

    [Fact]
    public void Build_keeps_partial_final_row()
    {
        var items =
            new[]
            {
                CreateItem("Alpha"),
                CreateItem("Bravo"),
                CreateItem("Charlie"),
                CreateItem("Delta"),
                CreateItem("Echo")
            };

        var result =
            LibraryGridRowBuilder.Build(
                items,
                columnCount: 2);

        Assert.Equal(
            3,
            result.Count);

        Assert.Equal(
            2,
            result[0].Items.Count);

        Assert.Equal(
            2,
            result[1].Items.Count);

        var finalItem =
            Assert.Single(
                result[2].Items);

        Assert.Same(
            items[4],
            finalItem);
    }

    [Fact]
    public void Build_preserves_original_item_order_across_rows()
    {
        var items =
            new[]
            {
                CreateItem("Delta"),
                CreateItem("Alpha"),
                CreateItem("Echo"),
                CreateItem("Bravo"),
                CreateItem("Charlie")
            };

        var result =
            LibraryGridRowBuilder.Build(
                items,
                columnCount: 2);

        var flattened =
            result
                .SelectMany(
                    row =>
                        row.Items)
                .ToArray();

        Assert.Equal(
            items,
            flattened);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Build_rejects_non_positive_column_count(
        int columnCount)
    {
        var items =
            new[]
            {
                CreateItem("Alpha")
            };

        Assert.Throws<
            ArgumentOutOfRangeException>(
            () =>
                LibraryGridRowBuilder.Build(
                    items,
                    columnCount));
    }

    [Fact]
    public void Build_rejects_null_items()
    {
        Assert.Throws<
            ArgumentNullException>(
            () =>
                LibraryGridRowBuilder.Build(
                    null!,
                    columnCount: 3));
    }

    private static LibraryItemViewModel CreateItem(
        string title)
    {
        return new LibraryItemViewModel(
            new GameId(
                Guid.NewGuid()),
            title,
            ProviderKind.Steam,
            "Steam",
            $@"D:\Games\{title}",
            InstalledSizeBytes: null);
    }
}
