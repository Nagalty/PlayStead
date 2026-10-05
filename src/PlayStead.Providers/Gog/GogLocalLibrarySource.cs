using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using PlayStead.Core.Library;
using PlayStead.Core.Scanning;

namespace PlayStead.Providers.Gog;

public sealed class GogLocalLibrarySource : ILocalLibrarySource
{
    private static readonly TimeSpan InstalledSizeCacheLifetime = TimeSpan.FromMinutes(2);
    private static readonly Regex InfoFileName = new(
        "^goggame-(?<id>[0-9]+)\\.info$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<string> _installationRoots;
    private readonly object _sizeCacheGate = new();
    private readonly Dictionary<string, InstalledSizeCacheEntry> _installedSizeCache =
        new(StringComparer.OrdinalIgnoreCase);
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
        => Task.Run(() => ScanCore(cancellationToken), cancellationToken);

    private SourceScanResult ScanCore(CancellationToken cancellationToken)
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

                    var launch = ResolveLaunch(info, installPath);
                    var installedSize = ResolveInstalledSize(installPath, observedAtUtc, cancellationToken);
                    if (!installedSize.HasValue)
                        warnings.Add($"{Path.GetFileName(infoPath)}: installed-size-unavailable");
                    installations.Add(DiscoveredInstallation.Create(
                        Provider,
                        externalId,
                        info.Name.Trim(),
                        installPath,
                        installedSizeBytes: installedSize,
                        observedAtUtc) with
                    {
                        ContentKind = InstallationContentKind.Game,
                        ExecutablePath = launch.ExecutablePath,
                        WorkingDirectory = launch.WorkingDirectory,
                        LaunchArguments = launch.Arguments
                    });
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or SecurityException)
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

        return SourceScanResult.Success(
            Provider,
            observedAtUtc,
            deduplicated,
            warnings);
    }

    private long? ResolveInstalledSize(
        string installPath,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        DateTime lastWriteTimeUtc;
        try
        {
            lastWriteTimeUtc = Directory.GetLastWriteTimeUtc(installPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }

        lock (_sizeCacheGate)
        {
            if (_installedSizeCache.TryGetValue(installPath, out var cached) &&
                cached.LastWriteTimeUtc == lastWriteTimeUtc &&
                observedAtUtc - cached.CachedAtUtc < InstalledSizeCacheLifetime)
            {
                return cached.SizeBytes;
            }
        }

        var calculated = CalculateInstalledSize(installPath, cancellationToken);
        lock (_sizeCacheGate)
        {
            _installedSizeCache[installPath] = new InstalledSizeCacheEntry(
                lastWriteTimeUtc, calculated, observedAtUtc);
        }
        return calculated;
    }

    private static long? CalculateInstalledSize(string installPath, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push(installPath);
        long total = 0;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryPath = pending.Pop();
            if (!visited.Add(directoryPath))
                continue;

            DirectoryInfo directory;
            try
            {
                directory = new DirectoryInfo(directoryPath);
                if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    continue;

                foreach (var entry in directory.EnumerateFileSystemInfos())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        continue;

                    if (entry is DirectoryInfo childDirectory)
                    {
                        pending.Push(childDirectory.FullName);
                        continue;
                    }

                    if (entry is FileInfo file)
                        total = checked(total + file.Length);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        return total;
    }

    private GogGameInfo ReadInfo(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<GogGameInfo>(stream, _jsonOptions)
            ?? throw new JsonException("GOG game info is empty.");
    }

    private static (string? ExecutablePath, string? WorkingDirectory, string? Arguments) ResolveLaunch(
        GogGameInfo info,
        string installPath)
    {
        var task = (info.PlayTasks ?? [])
            .Where(value => string.Equals(value.Category, "game", StringComparison.OrdinalIgnoreCase))
            .Where(value => !string.IsNullOrWhiteSpace(value.Path))
            .Select(value =>
            {
                var executablePath = Path.GetFullPath(Path.Combine(installPath, value.Path!));
                var workingDirectory = string.IsNullOrWhiteSpace(value.WorkingDir)
                    ? Path.GetDirectoryName(executablePath)
                    : Path.GetFullPath(Path.Combine(installPath, value.WorkingDir));
                return (Task: value, ExecutablePath: executablePath, WorkingDirectory: workingDirectory);
            })
            .FirstOrDefault(value => File.Exists(value.ExecutablePath));

        return task.Task is null
            ? (null, null, null)
            : (task.ExecutablePath, task.WorkingDirectory, task.Task.Arguments);
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
        public List<GogPlayTask>? PlayTasks { get; set; }
    }

    private sealed class GogPlayTask
    {
        public string? Category { get; set; }
        public string? Path { get; set; }
        public string? WorkingDir { get; set; }
        public string? Arguments { get; set; }
    }

    private sealed record InstalledSizeCacheEntry(
        DateTime LastWriteTimeUtc,
        long? SizeBytes,
        DateTimeOffset CachedAtUtc);
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
