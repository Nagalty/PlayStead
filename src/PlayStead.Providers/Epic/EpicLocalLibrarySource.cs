using System.Text.Json;
using System.Text.Json.Serialization;
using PlayStead.Core.Library;
using PlayStead.Core.Scanning;

namespace PlayStead.Providers.Epic;

public sealed class EpicLocalLibrarySource : ILocalLibrarySource
{
    private readonly string _manifestsDirectory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public EpicLocalLibrarySource(string manifestsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestsDirectory);
        _manifestsDirectory = Path.GetFullPath(manifestsDirectory);
    }

    public ProviderKind Provider => ProviderKind.Epic;

    public Task<SourceScanResult> ScanAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var observedAtUtc = DateTimeOffset.UtcNow;
        var installations = new List<DiscoveredInstallation>();
        var warnings = new List<string>();

        if (!Directory.Exists(_manifestsDirectory))
        {
            return Task.FromResult(SourceScanResult.Success(Provider, observedAtUtc, installations));
        }

        IEnumerable<string> manifestPaths;
        try
        {
            manifestPaths = Directory
                .EnumerateFiles(_manifestsDirectory, "*.item", SearchOption.TopDirectoryOnly)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(SourceScanResult.Success(
                Provider,
                observedAtUtc,
                installations,
                [$"Epic manifests: {ex.GetType().Name}"]));
        }

        foreach (var manifestPath in manifestPaths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var manifest = ReadManifest(manifestPath);
                if (!TryProject(manifest, observedAtUtc, out var installation, out var reason))
                {
                    if (!string.IsNullOrWhiteSpace(reason))
                        warnings.Add($"{Path.GetFileName(manifestPath)}: {reason}");
                    continue;
                }

                installations.Add(installation!);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                warnings.Add($"{Path.GetFileName(manifestPath)}: {ex.GetType().Name}");
            }
        }

        var deduplicated = installations
            .GroupBy(x => (x.Provider, x.ExternalId, x.InstallPath))
            .Select(x => x.First())
            .OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.ExternalId, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(SourceScanResult.Success(Provider, observedAtUtc, deduplicated, warnings));
    }

    private EpicManifest ReadManifest(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<EpicManifest>(stream, _jsonOptions)
            ?? throw new JsonException("Manifest is empty.");
    }

    private static bool TryProject(
        EpicManifest manifest,
        DateTimeOffset observedAtUtc,
        out DiscoveredInstallation? installation,
        out string? reason)
    {
        installation = null;
        reason = null;

        if (manifest.IsIncompleteInstall)
        {
            reason = "incomplete-install";
            return false;
        }

        var externalId = FirstNonEmpty(manifest.CatalogItemId, manifest.AppName);
        if (string.IsNullOrWhiteSpace(externalId))
        {
            reason = "missing-provider-id";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName))
        {
            reason = "missing-display-name";
            return false;
        }

        if (string.IsNullOrWhiteSpace(manifest.InstallLocation))
        {
            reason = "missing-install-location";
            return false;
        }

        string installPath;
        try
        {
            installPath = Path.GetFullPath(manifest.InstallLocation);
            if (!Directory.Exists(installPath))
            {
                reason = "install-path-missing";
                return false;
            }

            var info = new DirectoryInfo(installPath);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                reason = "install-path-reparse";
                return false;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            reason = $"install-path-{ex.GetType().Name}";
            return false;
        }

        installation = DiscoveredInstallation.Create(
            ProviderKind.Epic,
            externalId.Trim(),
            manifest.DisplayName.Trim(),
            installPath,
            manifest.InstallSize is >= 0 ? manifest.InstallSize : null,
            observedAtUtc) with
        {
            ContentKind = MapContentKind(manifest.TechnicalType, manifest.AppCategories),
            LaunchMetadata = new ProviderLaunchMetadata(
                ProviderKind.Epic,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["CatalogNamespace"] = manifest.CatalogNamespace?.Trim() ?? string.Empty,
                    ["CatalogItemId"] = manifest.CatalogItemId?.Trim() ?? string.Empty,
                    ["AppName"] = manifest.AppName?.Trim() ?? string.Empty,
                    ["TechnicalType"] = manifest.TechnicalType?.Trim() ?? string.Empty
                })
        };
        return true;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();

    private static InstallationContentKind MapContentKind(
        string? technicalType,
        IReadOnlyList<string>? appCategories)
    {
        var values = (appCategories ?? Array.Empty<string>())
            .Append(technicalType)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant())
            .ToArray();

        if (values.Any(value => value.Contains("engine", StringComparison.Ordinal) ||
                               value.Contains("sdk", StringComparison.Ordinal) ||
                               value.Contains("tool", StringComparison.Ordinal)))
            return InstallationContentKind.Tool;

        return InstallationContentKind.Game;
    }

    private sealed class EpicManifest
    {
        public string? DisplayName { get; set; }
        public string? InstallLocation { get; set; }
        public long? InstallSize { get; set; }
        public string? LaunchExecutable { get; set; }
        public string? CatalogItemId { get; set; }
        public string? AppName { get; set; }
        public string? CatalogNamespace { get; set; }
        public string? TechnicalType { get; set; }
        public List<string>? AppCategories { get; set; }
        [JsonPropertyName("bIsIncompleteInstall")]
        public bool IsIncompleteInstall { get; set; }
        public string? InstallationGuid { get; set; }
    }
}
