namespace PlayStead.Providers.Steam.Remote;

public sealed class SteamCmdPathResolver
{
    private readonly SteamCmdOptions _options;

    public SteamCmdPathResolver(
        SteamCmdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public string? Resolve()
    {
        if (!string.IsNullOrWhiteSpace(_options.ExecutablePath) &&
            File.Exists(_options.ExecutablePath))
        {
            return Path.GetFullPath(
                _options.ExecutablePath);
        }

        var localAppData =
            Environment.GetEnvironmentVariable(
                "LOCALAPPDATA");

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            var playSteadCandidate = Path.Combine(
                localAppData,
                "PlayStead",
                "Tools",
                "SteamCMD",
                "steamcmd.exe");

            if (File.Exists(playSteadCandidate))
            {
                return Path.GetFullPath(
                    playSteadCandidate);
            }
        }

        var pathValue =
            Environment.GetEnvironmentVariable(
                "PATH");

        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var rawDirectory in pathValue.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            var directory = rawDirectory.Trim('"');

            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var candidate = Path.Combine(
                directory,
                "steamcmd.exe");

            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }
}
