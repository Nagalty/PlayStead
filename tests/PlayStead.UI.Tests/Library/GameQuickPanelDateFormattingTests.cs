using PlayStead.UI.Library;
using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class GameQuickPanelDateFormattingTests
{
    private static DateTimeOffset Local(
        int year,
        int month,
        int day,
        int hour,
        int minute)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private static readonly DateTimeOffset Now = Local(2026, 9, 18, 12, 0);

    [Fact]
    public void Today_uses_today_label_and_preserves_source()
    {
        var source = Local(2026, 9, 18, 1, 12);
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
                Local(2026, 9, 17, 22, 34), Now));
    }

    [Fact]
    public void Older_date_in_same_year_uses_short_french_date()
    {
        Assert.Equal(
            "17 sept. à 23:47",
            GameQuickPanelViewModel.FormatTimestampForDisplay(
                Local(2026, 9, 17, 23, 47),
                Local(2026, 9, 19, 12, 0)));
    }

    [Fact]
    public void Date_in_another_year_includes_year()
    {
        Assert.Equal(
            "17 sept. 2025 à 23:47",
            GameQuickPanelViewModel.FormatTimestampForDisplay(
                Local(2025, 9, 17, 23, 47), Now));
    }
}
