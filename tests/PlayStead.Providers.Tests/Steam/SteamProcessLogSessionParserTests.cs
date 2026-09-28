using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamProcessLogSessionParserTests
{
    private readonly SteamProcessLogSessionParser _parser = new();

    [Fact]
    public void Dune_fixture_groups_overlapping_processes_into_one_session()
    {
        var result = _parser.Parse([
            "[2026-09-28 09:52:13] AppID 1172710 adding PID 5696 as a tracked process",
            "[2026-09-28 09:52:13] AppID 1172710 adding PID 68300 as a tracked process",
            "[2026-09-28 09:52:22] AppID 1172710 adding PID 101736 as a tracked process",
            "[2026-09-28 09:52:25] AppID 1172710 adding PID 39880 as a tracked process",
            "[2026-09-28 11:51:27] AppID 1172710 no longer tracking PID 39880, exit code 0",
            "[2026-09-28 11:51:27] AppID 1172710 no longer tracking PID 101736, exit code 39880",
            "[2026-09-28 11:51:27] AppID 1172710 no longer tracking PID 68300, exit code 0",
            "[2026-09-28 11:51:27] AppID 1172710 no longer tracking PID 5696, exit code 0"
        ]);

        var session = Assert.Single(result.Sessions);
        Assert.Equal("1172710", session.AppId);
        Assert.Equal(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(14), session.EndedAtUtc - session.StartedAtUtc);
        Assert.True(session.IsComplete);
    }

    [Fact]
    public void Duplicate_lines_do_not_duplicate_session()
    {
        var result = _parser.Parse([
            "[2026-09-28 09:00:00] AppID 1 adding PID 10 as a tracked process",
            "[2026-09-28 09:00:00] AppID 1 adding PID 10 as a tracked process",
            "[2026-09-28 09:30:00] AppID 1 no longer tracking PID 10, exit code 0",
            "[2026-09-28 09:30:00] AppID 1 no longer tracking PID 10, exit code 0"
        ]);

        Assert.Single(result.Sessions);
    }

    [Fact]
    public void Distinct_launches_and_appids_remain_distinct()
    {
        var result = _parser.Parse([
            "[2026-09-28 09:00:00] AppID 1 adding PID 10 as a tracked process",
            "[2026-09-28 09:05:00] AppID 1 no longer tracking PID 10, exit code 0",
            "[2026-09-28 10:00:00] AppID 1 adding PID 11 as a tracked process",
            "[2026-09-28 10:05:00] AppID 1 no longer tracking PID 11, exit code 0",
            "[2026-09-28 11:00:00] AppID 2 adding PID 20 as a tracked process",
            "[2026-09-28 11:05:00] AppID 2 no longer tracking PID 20, exit code 0"
        ]);

        Assert.Equal(3, result.Sessions.Count);
    }

    [Fact]
    public void Missing_end_is_incomplete_and_missing_start_is_incomplete()
    {
        var result = _parser.Parse([
            "[2026-09-28 09:00:00] AppID 1 adding PID 10 as a tracked process",
            "[2026-09-28 10:00:00] AppID 2 no longer tracking PID 20, exit code 0"
        ]);

        Assert.Equal(2, result.Sessions.Count);
        Assert.Contains(result.Sessions, value => !value.IsComplete && value.StartedAtUtc is not null);
        Assert.Contains(result.Sessions, value => !value.IsComplete && value.StartedAtUtc is null);
    }
}
