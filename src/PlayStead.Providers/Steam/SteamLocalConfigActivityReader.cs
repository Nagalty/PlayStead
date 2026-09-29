using System.Globalization;
using PlayStead.Providers.Steam.ValveText;

namespace PlayStead.Providers.Steam;

public sealed record SteamLocalConfigLifetime(
    long PlaytimeMinutes,
    long PlaytimeDisconnectedMinutes)
{
    public TimeSpan Total => TimeSpan.FromMinutes(checked(
        PlaytimeMinutes + PlaytimeDisconnectedMinutes));
}

/// <summary>
/// Reads the current Steam account's lifetime counters from localconfig.vdf.
/// This reader is deliberately read-only and does not interpret opaque blobs.
/// </summary>
public sealed class SteamLocalConfigActivityReader
{
    public IReadOnlyDictionary<string, SteamLocalConfigLifetime> Read(
        string steamRoot,
        IReadOnlyCollection<string> appIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(steamRoot);
        ArgumentNullException.ThrowIfNull(appIds);

        var accountId = SelectCurrentAccountId(steamRoot);
        if (accountId is null)
            return new Dictionary<string, SteamLocalConfigLifetime>(StringComparer.Ordinal);

        var path = Path.Combine(
            steamRoot,
            "userdata",
            accountId,
            "config",
            "localconfig.vdf");
        if (!File.Exists(path))
            return new Dictionary<string, SteamLocalConfigLifetime>(StringComparer.Ordinal);

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var root = ValveTextParser.Parse(reader.ReadToEnd());
            var apps = FindApps(root);
            var requested = appIds.ToHashSet(StringComparer.Ordinal);
            var result = new Dictionary<string, SteamLocalConfigLifetime>(StringComparer.Ordinal);
            foreach (var pair in apps)
            {
                if (!requested.Contains(pair.Key) ||
                    pair.Value is not IReadOnlyDictionary<string, object> app ||
                    !TryNonNegativeLong(app, "Playtime", out var playtime))
                {
                    continue;
                }

                var disconnected = 0L;
                if (app.TryGetValue("PlaytimeDisconnected", out var rawDisconnected))
                {
                    if (!TryNonNegativeLong(rawDisconnected, out disconnected))
                        continue;
                }

                try
                {
                    _ = checked(playtime + disconnected);
                    _ = TimeSpan.FromMinutes(playtime + disconnected);
                    result[pair.Key] = new SteamLocalConfigLifetime(playtime, disconnected);
                }
                catch (OverflowException)
                {
                    // Invalid/overflowing counters are unknown, never truncated.
                }
            }

            return result;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return new Dictionary<string, SteamLocalConfigLifetime>(StringComparer.Ordinal);
        }
    }

    internal static string? SelectCurrentAccountId(string steamRoot)
    {
        var loginUsersPath = Path.Combine(steamRoot, "config", "loginusers.vdf");
        if (!File.Exists(loginUsersPath))
            return null;

        try
        {
            using var stream = new FileStream(
                loginUsersPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var root = ValveTextParser.Parse(reader.ReadToEnd());
            if (!root.TryGetValue("users", out var rawUsers) ||
                rawUsers is not IReadOnlyDictionary<string, object> users)
            {
                return null;
            }

            var candidates = users
                .Where(pair => pair.Value is IReadOnlyDictionary<string, object>)
                .Select(pair => new
                {
                    SteamId64 = pair.Key,
                    Values = (IReadOnlyDictionary<string, object>)pair.Value
                })
                .ToArray();
            var mostRecent = candidates
                .Where(candidate => IsTrue(candidate.Values, "MostRecent"))
                .ToArray();
            if (mostRecent.Length == 1 && TrySteamId64ToAccountId(mostRecent[0].SteamId64, out var recentId))
                return recentId;
            if (candidates.Length == 1 && TrySteamId64ToAccountId(candidates[0].SteamId64, out var onlyId))
                return onlyId;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }

        return null;
    }

    internal static bool TrySteamId64ToAccountId(string value, out string accountId)
    {
        accountId = string.Empty;
        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var steamId64) ||
            steamId64 < 76561197960265728UL)
        {
            return false;
        }

        accountId = (steamId64 - 76561197960265728UL).ToString(CultureInfo.InvariantCulture);
        return accountId != "0";
    }

    private static IReadOnlyDictionary<string, object> FindApps(
        IReadOnlyDictionary<string, object> root)
    {
        if (root.TryGetValue("UserLocalConfigStore", out var store) &&
            store is IReadOnlyDictionary<string, object> storeBlock)
        {
            root = storeBlock;
        }

        if (TryGetBlock(root, "Software", out var software) &&
            TryGetBlock(software, "Valve", out var valve) &&
            TryGetBlock(valve, "Steam", out var steam) &&
            TryGetBlock(steam, "apps", out var apps))
        {
            return apps;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryGetBlock(
        IReadOnlyDictionary<string, object> values,
        string key,
        out IReadOnlyDictionary<string, object> block)
    {
        if (values.TryGetValue(key, out var value) &&
            value is IReadOnlyDictionary<string, object> typed)
        {
            block = typed;
            return true;
        }

        block = new Dictionary<string, object>();
        return false;
    }

    private static bool TryNonNegativeLong(
        IReadOnlyDictionary<string, object> values,
        string key,
        out long value)
    {
        value = 0;
        return values.TryGetValue(key, out var raw) && TryNonNegativeLong(raw, out value);
    }

    private static bool TryNonNegativeLong(object raw, out long value)
    {
        if (raw is string text &&
            long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
            value >= 0)
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool IsTrue(IReadOnlyDictionary<string, object> values, string key) =>
        values.TryGetValue(key, out var raw) &&
        raw is string text &&
        (text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase));
}
