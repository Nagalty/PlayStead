using System.Globalization;
using System.Text.RegularExpressions;

namespace PlayStead.Providers.Steam;

public sealed partial class SteamProcessLogSessionParser
{
    private static readonly TimeSpan HandoffTolerance = TimeSpan.FromSeconds(30);

    public SteamProcessLogParseResult Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var events = new List<ProcessEvent>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var match = EventPattern().Match(line);
            if (!match.Success ||
                !DateTimeOffset.TryParse(
                    match.Groups["timestamp"].Value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var timestamp))
            {
                continue;
            }

            var kind = match.Groups["kind"].Value.StartsWith("adding", StringComparison.Ordinal)
                ? ProcessEventKind.Start
                : ProcessEventKind.End;
            var key = $"{timestamp:O}|{match.Groups["appid"].Value}|{match.Groups["pid"].Value}|{kind}";
            if (seen.Add(key))
            {
                events.Add(new ProcessEvent(
                    match.Groups["appid"].Value,
                    match.Groups["pid"].Value,
                    timestamp.ToUniversalTime(),
                    kind));
            }
        }

        var sessions = new List<SteamProcessLogSession>();
        foreach (var appEvents in events.GroupBy(value => value.AppId, StringComparer.Ordinal))
        {
            var active = new HashSet<string>(StringComparer.Ordinal);
            DateTimeOffset? started = null;
            DateTimeOffset? lastEnded = null;
            foreach (var item in appEvents.OrderBy(value => value.TimestampUtc))
            {
                if (item.Kind == ProcessEventKind.Start)
                {
                    if (started is not null &&
                        lastEnded is not null &&
                        active.Count == 0 &&
                        item.TimestampUtc - lastEnded > HandoffTolerance)
                    {
                        sessions.Add(new SteamProcessLogSession(appEvents.Key, started, lastEnded, true));
                        started = null;
                        lastEnded = null;
                    }

                    started ??= item.TimestampUtc;
                    active.Add(item.Pid);
                }
                else if (!active.Remove(item.Pid))
                {
                    sessions.Add(new SteamProcessLogSession(appEvents.Key, null, item.TimestampUtc, false));
                }
                else if (active.Count == 0)
                {
                    lastEnded = item.TimestampUtc;
                }
            }

            if (started is not null)
            {
                sessions.Add(new SteamProcessLogSession(
                    appEvents.Key,
                    started,
                    active.Count == 0 ? lastEnded : null,
                    active.Count == 0));
            }
        }

        var timestamps = events.Select(value => value.TimestampUtc).OrderBy(value => value).ToArray();
        return new SteamProcessLogParseResult(
            sessions.OrderBy(value => value.StartedAtUtc ?? value.EndedAtUtc).ToArray(),
            new SteamProcessLogCoverage(
                timestamps.Length == 0 ? null : timestamps[0],
                timestamps.Length == 0 ? null : timestamps[^1]));
    }

    [GeneratedRegex("^\\[(?<timestamp>[^]]+)\\]\\s+AppID\\s+(?<appid>\\d+)\\s+(?<kind>adding PID|no longer tracking PID)\\s+(?<pid>\\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex EventPattern();

    private enum ProcessEventKind { Start, End }
    private sealed record ProcessEvent(string AppId, string Pid, DateTimeOffset TimestampUtc, ProcessEventKind Kind);
}

public sealed record SteamProcessLogParseResult(
    IReadOnlyList<SteamProcessLogSession> Sessions,
    SteamProcessLogCoverage Coverage);
