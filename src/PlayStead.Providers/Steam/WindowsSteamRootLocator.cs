using Microsoft.Win32;

namespace PlayStead.Providers.Steam;

public sealed class WindowsSteamRootLocator
{
    private readonly IReadOnlyList<string?>? _candidatesOverride;

    public WindowsSteamRootLocator()
    {
    }

    public WindowsSteamRootLocator(IEnumerable<string?> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        _candidatesOverride = candidates.ToArray();
    }

    public string? TryLocate()
    {
        foreach (var candidate in GetCandidates())
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private IEnumerable<string?> GetCandidates() =>
        _candidatesOverride ?? ReadRegistryCandidates();

    private static IEnumerable<string?> ReadRegistryCandidates()
    {
        yield return ReadRegistryValue(
            Registry.CurrentUser,
            @"Software\Valve\Steam",
            "SteamPath");

        yield return ReadRegistryValue(
            Registry.LocalMachine,
            @"SOFTWARE\WOW6432Node\Valve\Steam",
            "InstallPath");

        yield return ReadRegistryValue(
            Registry.LocalMachine,
            @"SOFTWARE\Valve\Steam",
            "InstallPath");
    }

    private static string? ReadRegistryValue(
        RegistryKey root,
        string subKey,
        string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(
                subKey,
                writable: false);

            return key?.GetValue(valueName) as string;
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException)
        {
            return null;
        }
    }
}
