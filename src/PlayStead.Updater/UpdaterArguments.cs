namespace PlayStead.Updater;

public sealed record UpdaterArguments(int ProcessId, string PackagePath, string TargetDirectory, string ExecutablePath, string Version, string? UpdatesRoot = null)
{
    public static bool TryParse(string[] args, out UpdaterArguments? result, out string? error)
    {
        result = null; error = null;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            { error = "Invalid updater arguments."; return false; }
            values[args[i]] = args[++i];
        }
        if (!values.TryGetValue("--pid", out var pidText) || !int.TryParse(pidText, out var pid) || pid < 0 ||
            !values.TryGetValue("--package", out var package) ||
            !values.TryGetValue("--target-dir", out var target) ||
            !values.TryGetValue("--exe", out var exe) ||
            !values.TryGetValue("--version", out var version) || string.IsNullOrWhiteSpace(version) ||
            !values.TryGetValue("--updates-root", out var updatesRoot) || string.IsNullOrWhiteSpace(updatesRoot))
        { error = "Required updater arguments are missing."; return false; }
        result = new UpdaterArguments(pid, package, target, exe, version, updatesRoot);
        return true;
    }
}
