using System.Diagnostics;
using PlayStead.Core.Library;

namespace PlayStead.Core.Graphics;

public sealed class LocalGraphicsTechnologyDetectionService : IGraphicsTechnologyDetectionService
{
    private readonly IGraphicsTechnologySupportSource? _supportSource;

    public LocalGraphicsTechnologyDetectionService(IGraphicsTechnologySupportSource? supportSource = null)
    {
        _supportSource = supportSource;
    }

    private static readonly IReadOnlySet<string> AllowedPluginDirectoryNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DLSS", "StreamlineCore", "Marketplace", "XeSS", "Rendering",
            "Nvidia", "NVIDIA", "Intel", "Binaries", "ThirdParty", "Win64"
        };

    private static readonly IReadOnlyDictionary<GraphicsTechnology, string[]> RuntimeFiles =
        new Dictionary<GraphicsTechnology, string[]>
        {
            [GraphicsTechnology.DLSS] = ["nvngx_dlss.dll", "nvngx_dlssg.dll"],
            [GraphicsTechnology.XeSS] = ["libxess.dll", "libxess_dx11.dll", "libxess_fg.dll"],
            [GraphicsTechnology.FSR] = [
                "amd_fidelityfx_dx12.dll",
                "ffx_fsr2_api_dx12_x64.dll",
                "ffx_fsr2_api_x64.dll",
                "ffx_fsr3_api_dx12_x64.dll"
            ]
        };

    public Task<IReadOnlyList<GraphicsTechnologyObservation>> DetectAsync(
        GameId gameId,
        string? installPath,
        CancellationToken cancellationToken)
        => DetectAsync(gameId, installPath, null, cancellationToken);

    public async Task<IReadOnlyList<GraphicsTechnologyObservation>> DetectAsync(
        GameId gameId,
        string? installPath,
        string? gameTitle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
            return await MergeOfficialSupportAsync(gameId, gameTitle, [], cancellationToken);

        var root = Path.GetFullPath(installPath);
        var locations = GetBoundedLocations(root, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var result = new List<GraphicsTechnologyObservation>();

        foreach (var technology in new[] { GraphicsTechnology.DLSS, GraphicsTechnology.FSR, GraphicsTechnology.XeSS })
        {
            var evidence = new List<GraphicsTechnologyEvidence>();
            var remainingMarkers = RuntimeFiles.GetValueOrDefault(technology, []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var location in locations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var fileName in remainingMarkers.ToArray())
                {
                    var path = Path.Combine(location, fileName);
                    if (AddRuntimeFile(evidence, gameId, technology, path, now))
                        remainingMarkers.Remove(fileName);
                }

                if (remainingMarkers.Count == 0)
                    break;
            }

            var version = evidence
                .Select(x => x.EvidencePath)
                .Select(TryReadVersion)
                .FirstOrDefault(x => x is not null);
            if (version is not null)
                evidence.Add(new GraphicsTechnologyEvidence(gameId, technology, GraphicsEvidenceKind.RuntimeVersionInfo, "runtime metadata", now, 1.0, version));

            result.Add(CreateObservation(gameId, technology, evidence, locations, version));
        }

        var dlssEvidence = result.First(x => x.Technology == GraphicsTechnology.DLSS).Evidence;
        var fsrEvidence = result.First(x => x.Technology == GraphicsTechnology.FSR).Evidence;
        var frameGenerationEvidence = dlssEvidence
            .Where(x => string.Equals(Path.GetFileName(x.EvidencePath), "nvngx_dlssg.dll", StringComparison.OrdinalIgnoreCase))
            .Concat(fsrEvidence.Where(x => string.Equals(Path.GetFileName(x.EvidencePath), "ffx_fsr3_api_dx12_x64.dll", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (frameGenerationEvidence.Length > 0)
        {
            result.Add(new GraphicsTechnologyObservation(
                gameId,
                GraphicsTechnology.FrameGeneration,
                GraphicsSupportStatus.Unknown,
                GraphicsRuntimeStatus.Present,
                GraphicsActivationStatus.Unknown,
                null,
                frameGenerationEvidence,
                locations,
                GraphicsDetectionCoverage.PartialKnownLocations,
                [new GraphicsTechnologySupportEvidence(
                    gameId,
                    GraphicsTechnology.FrameGeneration,
                    GraphicsSupportStatus.Supported,
                    GraphicsTechnologySupportSourceKind.LocalEvidence,
                    "LocalEvidence",
                    frameGenerationEvidence[0].ObservedAtUtc)]));
        }
        result.Add(new GraphicsTechnologyObservation(
            gameId,
            GraphicsTechnology.DirectX12,
            GraphicsSupportStatus.Unknown,
            GraphicsRuntimeStatus.Unknown,
            GraphicsActivationStatus.Unknown,
            null,
            [],
            locations,
            GraphicsDetectionCoverage.PartialKnownLocations));
        return await MergeOfficialSupportAsync(gameId, gameTitle, result, cancellationToken);
    }

    private async Task<IReadOnlyList<GraphicsTechnologyObservation>> MergeOfficialSupportAsync(
        GameId gameId,
        string? gameTitle,
        IReadOnlyList<GraphicsTechnologyObservation> local,
        CancellationToken cancellationToken)
    {
        if (_supportSource is null || string.IsNullOrWhiteSpace(gameTitle))
            return local;

        IReadOnlyList<GraphicsTechnologySupportEvidence> support;
        try
        {
            support = await _supportSource.GetSupportAsync(gameTitle, gameId, cancellationToken);
        }
        catch (HttpRequestException) { return local; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return local; }

        var result = local.ToList();
        foreach (var evidence in support.Where(x => x.SupportStatus == GraphicsSupportStatus.Supported))
        {
            var current = result.FirstOrDefault(x => x.Technology == evidence.Technology);
            if (current is not null && current.RuntimeStatus == GraphicsRuntimeStatus.Present)
                continue;

            var index = current is null ? -1 : result.IndexOf(current);
            var observation = new GraphicsTechnologyObservation(
                gameId,
                evidence.Technology,
                GraphicsSupportStatus.Supported,
                current?.RuntimeStatus ?? GraphicsRuntimeStatus.Unknown,
                GraphicsActivationStatus.Unknown,
                current?.RuntimeVersion,
                current?.Evidence ?? [],
                current?.ScannedLocations,
                current?.DetectionCoverage ?? GraphicsDetectionCoverage.PartialKnownLocations,
                [evidence],
                evidence.Technology == GraphicsTechnology.FrameGeneration ? "FSR Frame Generation" : null);
            if (index >= 0)
                result[index] = observation;
            else
                result.Add(observation);
        }

        return result;
    }

    private static GraphicsTechnologyObservation CreateObservation(
        GameId gameId,
        GraphicsTechnology technology,
        IReadOnlyList<GraphicsTechnologyEvidence> evidence,
        IReadOnlyList<string> locations,
        string? version) =>
        new(
            gameId,
            technology,
            evidence.Count == 0 ? GraphicsSupportStatus.Unknown : GraphicsSupportStatus.Supported,
            evidence.Count == 0 ? GraphicsRuntimeStatus.Unknown : GraphicsRuntimeStatus.Present,
            GraphicsActivationStatus.Unknown,
            version,
            evidence,
            locations,
            GraphicsDetectionCoverage.PartialKnownLocations,
            evidence.Count == 0
                ? []
                : [new GraphicsTechnologySupportEvidence(
                    gameId,
                    technology,
                    GraphicsSupportStatus.Supported,
                    GraphicsTechnologySupportSourceKind.LocalEvidence,
                    "LocalEvidence",
                    evidence[0].ObservedAtUtc)]);

    private static IReadOnlyList<string> GetBoundedLocations(string root, CancellationToken cancellationToken)
    {
        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddLocation(locations, root);
        AddLocation(locations, Path.Combine(root, "Binaries", "Win64"));
        AddLocation(locations, Path.Combine(root, "Engine", "Binaries", "ThirdParty"));
        AddLocation(locations, Path.Combine(root, "Engine", "Plugins"));
        AddBoundedPluginTree(locations, Path.Combine(root, "Engine", "Plugins"), cancellationToken);

        try
        {
            foreach (var child in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddLocation(locations, Path.Combine(child, "Binaries", "Win64"));
                var plugins = Path.Combine(child, "Plugins");
                AddLocation(locations, plugins);
                AddBoundedPluginTree(locations, plugins, cancellationToken);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
        return locations.ToArray();
    }

    private static void AddBoundedPluginTree(
        HashSet<string> locations,
        string pluginRoot,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(pluginRoot))
            return;

        var pending = new Queue<(string Path, int Depth)>();
        if (IsSafeDirectory(pluginRoot))
            pending.Enqueue((pluginRoot, 0));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (current, depth) = pending.Dequeue();
            AddLocation(locations, current);
            if (depth >= 8)
                continue;

            try
            {
                foreach (var child in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(child);
                    if (string.Equals(name, "Content", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "Paks", StringComparison.OrdinalIgnoreCase) ||
                        !AllowedPluginDirectoryNames.Contains(name) ||
                        !IsSafeDirectory(child))
                        continue;
                    pending.Enqueue((child, depth + 1));
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
    }

    private static bool IsSafeDirectory(string path)
    {
        try
        {
            return !new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
    }

    private static void AddLocation(HashSet<string> locations, string path)
    {
        try
        {
            if (Directory.Exists(path) && !new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint))
                locations.Add(path);
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    private static bool AddRuntimeFile(List<GraphicsTechnologyEvidence> evidence, GameId gameId, GraphicsTechnology technology, string path, DateTimeOffset observedAtUtc)
    {
        try
        {
            if (File.Exists(path))
            {
                evidence.Add(new GraphicsTechnologyEvidence(gameId, technology, GraphicsEvidenceKind.RuntimeFilePresent, path, observedAtUtc, 1.0));
                return true;
            }
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
        return false;
    }

    private static string? TryReadVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return string.IsNullOrWhiteSpace(info.FileVersion) ? null : info.FileVersion;
        }
        catch (FileNotFoundException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (IOException) { return null; }
    }
}
