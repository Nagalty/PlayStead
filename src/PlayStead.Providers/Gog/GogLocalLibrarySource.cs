using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using PlayStead.Core.Library;
using PlayStead.Core.Scanning;

namespace PlayStead.Providers.Gog;

public sealed class GogLocalLibrarySource : ILocalLibrarySource
{
    private static readonly Regex InfoFileName = new(
        "^goggame-(?<id>[0-9]+)\\.info$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<string> _installationRoots;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public GogLocalLibrarySource(IEnumerable<string> installationRoots)
    {
        ArgumentNullException.ThrowIfNull(installationRoots);
        _installationRoots = installationRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ProviderKind Provider => ProviderKind.Gog;

    public Task<SourceScanResult> ScanAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var observedAtUtc = DateTimeOffset.UtcNow;
        var installations = new List<DiscoveredInstallation>();
        var warnings = new List<string>();

        foreach (var root in _installationRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root))
                continue;

            IEnumerable<string> infoPaths;
            try
            {
                infoPaths = Directory
                    .EnumerateFiles(root, "goggame-*.info", SearchOption.TopDirectoryOnly)
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"{root}: {ex.GetType().Name}");
                continue;
            }

            foreach (var infoPath in infoPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!TryGetExternalId(infoPath, out var externalId))
                    {
                        warnings.Add($"{Path.GetFileName(infoPath)}: missing-provider-id");
                        continue;
                    }

                    var info = ReadInfo(infoPath);
                    if (string.IsNullOrWhiteSpace(info.Name))
                    {
                        warnings.Add($"{Path.GetFileName(infoPath)}: missing-display-name");
                        continue;
                    }

                    var installPath = Path.GetFullPath(Path.GetDirectoryName(infoPath)!);
                    if (!Directory.Exists(installPath))
                    {
                        warnings.Add($"{Path.GetFileName(infoPath)}: install-path-missing");
                        continue;
                    }

                    var directory = new DirectoryInfo(installPath);
                    if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        warnings.Add($"{Path.GetFileName(infoPath)}: install-path-reparse");
                        continue;
                    }

                    installations.Add(DiscoveredInstallation.Create(
                        Provider,
                        externalId,
                        info.Name.Trim(),
                        installPath,
                        installedSizeBytes: null,
                        observedAtUtc) with
                    {
                        ContentKind = InstallationContentKind.Game
                    });
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"{Path.GetFileName(infoPath)}: {ex.GetType().Name}");
                }
            }
        }

        var deduplicated = installations
            .GroupBy(item => (item.Provider, item.ExternalId, item.InstallPath))
            .Select(group => group.First())
            .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.ExternalId, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(SourceScanResult.Success(
            Provider,
            observedAtUtc,
            deduplicated,
            warnings));
    }

    private GogGameInfo ReadInfo(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<GogGameInfo>(stream, _jsonOptions)
            ?? throw new JsonException("GOG game info is empty.");
    }

    private static bool TryGetExternalId(string path, out string externalId)
    {
        var match = InfoFileName.Match(Path.GetFileName(path));
        externalId = match.Success ? match.Groups["id"].Value : string.Empty;
        return !string.IsNullOrWhiteSpace(externalId);
    }

    private sealed class GogGameInfo
    {
        public string? Name { get; set; }
    }
}

public static class GogRegistryInstallRootReader
{
    private const string GamesKey = @"SOFTWARE\WOW6432Node\GOG.com\Games";

    public static IReadOnlyList<string> ReadInstallRoots()
    {
        var roots = new List<string>();
        try
        {
            using var games = Registry.LocalMachine.OpenSubKey(GamesKey, writable: false);
            if (games is null)
                return roots;

            foreach (var name in games.GetSubKeyNames())
            {
                using var game = games.OpenSubKey(name, writable: false);
                if (game?.GetValue("path") is string path && !string.IsNullOrWhiteSpace(path))
                    roots.Add(path);
            }
        }
        catch (SecurityException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }

        return roots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
