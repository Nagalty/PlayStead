using PlayStead.UI.Library;
using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class GameQuickPanelDateFormattingTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 12, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Today_uses_today_label_and_preserves_source()
    {
        var source = new DateTimeOffset(2026, 9, 18, 1, 12, 0, TimeSpan.FromHours(2));
        var result = GameQuickPanelViewModel.FormatTimestampForDisplay(source, Now);

        Assert.Equal("aujourd’hui à 01:12", result);
        Assert.Equal(1, source.Hour);
    }

    [Fact]
    public void Yesterday_uses_yesterday_label()
    {
        Assert.Equal(
            "hier à 22:34",
            GameQuickPanelViewModel.FormatTimestampForDisplay(
                new DateTimeOffset(2026, 9, 17, 22, 34, 0, TimeSpan.FromHours(2)), Now));
    }

    [Fact]
    public void Older_date_in_same_year_uses_short_french_date()
    {
        Assert.Equal(
            "17 sept. à 23:47",
            GameQuickPanelViewModel.FormatTimestampForDisplay(
                new DateTimeOffset(2026, 9, 17, 23, 47, 0, TimeSpan.FromHours(2)),
                new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.FromHours(2))));
    }

    [Fact]
    public void Date_in_another_year_includes_year()
    {
        Assert.Equal(
            "17 sept. 2025 à 23:47",
            GameQuickPanelViewModel.FormatTimestampForDisplay(
                new DateTimeOffset(2025, 9, 17, 23, 47, 0, TimeSpan.FromHours(2)), Now));
    }
}
