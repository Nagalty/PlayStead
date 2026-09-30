using PlayStead.Core.Graphics;
using PlayStead.Core.Library;

namespace PlayStead.Core.Tests.Graphics;

public sealed class LocalGraphicsTechnologyDetectionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-Graphics-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Known_runtime_files_are_present_but_activation_remains_unknown()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "nvngx_dlss.dll"), [1]);
        File.WriteAllBytes(Path.Combine(_root, "libxess.dll"), [1]);
        File.WriteAllBytes(Path.Combine(_root, "ffx_fsr3_api_dx12_x64.dll"), [1]);

        var result = await DetectAsync();

        Assert.Equal(GraphicsRuntimeStatus.Present, Find(result, GraphicsTechnology.DLSS).RuntimeStatus);
        Assert.Equal(GraphicsRuntimeStatus.Present, Find(result, GraphicsTechnology.XeSS).RuntimeStatus);
        Assert.Equal(GraphicsRuntimeStatus.Present, Find(result, GraphicsTechnology.FSR).RuntimeStatus);
        Assert.All(result.Where(x => x.RuntimeStatus == GraphicsRuntimeStatus.Present), x => Assert.Equal(GraphicsActivationStatus.Unknown, x.ActivationStatus));
    }

    [Fact]
    public async Task Missing_runtime_is_never_reported_as_active_or_unsupported()
    {
        Directory.CreateDirectory(_root);

        var result = await DetectAsync();

        var dlss = Find(result, GraphicsTechnology.DLSS);
        Assert.Equal(GraphicsRuntimeStatus.Unknown, dlss.RuntimeStatus);
        Assert.Equal(GraphicsSupportStatus.Unknown, dlss.SupportStatus);
        Assert.Equal(GraphicsActivationStatus.Unknown, dlss.ActivationStatus);
    }

    [Fact]
    public async Task Detection_is_bounded_to_known_install_subdirectories()
    {
        Directory.CreateDirectory(Path.Combine(_root, "UnknownNested"));
        File.WriteAllBytes(Path.Combine(_root, "UnknownNested", "nvngx_dlss.dll"), [1]);

        var result = await DetectAsync();

        Assert.Equal(GraphicsRuntimeStatus.Unknown, Find(result, GraphicsTechnology.DLSS).RuntimeStatus);
    }

    [Fact]
    public async Task Known_nested_locations_are_scanned_without_scanning_content_or_paks()
    {
        var binaries = Path.Combine(_root, "Binaries", "Win64");
        Directory.CreateDirectory(binaries);
        File.WriteAllBytes(Path.Combine(binaries, "nvngx_dlss.dll"), [1]);
        Directory.CreateDirectory(Path.Combine(_root, "Content", "Paks"));
        File.WriteAllBytes(Path.Combine(_root, "Content", "Paks", "nvngx_dlss.dll"), [1]);

        var result = await DetectAsync();
        var dlss = Find(result, GraphicsTechnology.DLSS);

        Assert.Equal(GraphicsRuntimeStatus.Present, dlss.RuntimeStatus);
        Assert.Contains(dlss.ScannedLocationList, x => x.EndsWith(Path.Combine("Binaries", "Win64"), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dlss.ScannedLocationList, x => x.Contains("Content", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dlss.Evidence, x => x.EvidencePath.Contains("Content", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Frame_generation_runtime_is_present_but_activation_is_unknown()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "nvngx_dlssg.dll"), [1]);

        var frameGeneration = Find(await DetectAsync(), GraphicsTechnology.FrameGeneration);

        Assert.Equal(GraphicsRuntimeStatus.Present, frameGeneration.RuntimeStatus);
        Assert.Equal(GraphicsActivationStatus.Unknown, frameGeneration.ActivationStatus);
    }

    [Fact]
    public async Task Deep_plugin_runtime_locations_are_detected_within_bound()
    {
        var dlss = Path.Combine(_root, "Engine", "Plugins", "DLSS", "Binaries", "ThirdParty", "Win64");
        var xess = Path.Combine(_root, "Game", "Plugins", "Rendering", "Intel", "XeSS", "Binaries", "ThirdParty", "Win64");
        Directory.CreateDirectory(dlss);
        Directory.CreateDirectory(xess);
        File.WriteAllBytes(Path.Combine(dlss, "nvngx_dlss.dll"), [1]);
        File.WriteAllBytes(Path.Combine(dlss, "nvngx_dlssg.dll"), [1]);
        File.WriteAllBytes(Path.Combine(xess, "libxess.dll"), [1]);

        var result = await DetectAsync();

        Assert.Equal(GraphicsRuntimeStatus.Present, Find(result, GraphicsTechnology.DLSS).RuntimeStatus);
        Assert.Equal(GraphicsRuntimeStatus.Present, Find(result, GraphicsTechnology.FrameGeneration).RuntimeStatus);
        Assert.Equal(GraphicsRuntimeStatus.Present, Find(result, GraphicsTechnology.XeSS).RuntimeStatus);
        Assert.All(result.Where(x => x.RuntimeStatus == GraphicsRuntimeStatus.Present), x => Assert.Equal(GraphicsActivationStatus.Unknown, x.ActivationStatus));
    }

    [Fact]
    public async Task Plugin_scan_stops_at_maximum_depth()
    {
        var current = Path.Combine(_root, "Engine", "Plugins");
        for (var i = 0; i < 9; i++)
        {
            current = Path.Combine(current, "Level" + i);
            Directory.CreateDirectory(current);
        }
        File.WriteAllBytes(Path.Combine(current, "nvngx_dlss.dll"), [1]);

        var dlss = Find(await DetectAsync(), GraphicsTechnology.DLSS);

        Assert.Equal(GraphicsRuntimeStatus.Unknown, dlss.RuntimeStatus);
    }

    [Fact]
    public async Task Additional_known_markers_are_supported()
    {
        var gameBinaries = Path.Combine(_root, "DuneGame", "Binaries", "Win64");
        Directory.CreateDirectory(gameBinaries);
        File.WriteAllBytes(Path.Combine(gameBinaries, "libxess_fg.dll"), [1]);
        File.WriteAllBytes(Path.Combine(gameBinaries, "ffx_fsr2_api_x64.dll"), [1]);

        var result = await DetectAsync();

        Assert.Equal(GraphicsRuntimeStatus.Present, Find(result, GraphicsTechnology.XeSS).RuntimeStatus);
        Assert.Equal(GraphicsRuntimeStatus.Present, Find(result, GraphicsTechnology.FSR).RuntimeStatus);
    }

    [Fact]
    public async Task User_projection_uses_availability_language_and_specific_frame_generation_label()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "nvngx_dlssg.dll"), [1]);

        var result = await DetectAsync();
        var dlss = Find(result, GraphicsTechnology.DLSS);
        var frameGeneration = Find(result, GraphicsTechnology.FrameGeneration);
        var fsr = Find(result, GraphicsTechnology.FSR);

        Assert.Equal("Disponible", dlss.RuntimeStatusLabel);
        Assert.Equal("DLSS Frame Generation", frameGeneration.TechnologyLabel);
        Assert.Equal("État inconnu", fsr.RuntimeStatusLabel);
    }

    [Fact]
    public async Task Official_support_fills_missing_local_runtime_without_claiming_runtime_presence()
    {
        Directory.CreateDirectory(_root);
        var service = new LocalGraphicsTechnologyDetectionService(new FakeSupportSource(
            new GraphicsTechnologySupportEvidence(GameId.New(), GraphicsTechnology.FSR, GraphicsSupportStatus.Supported,
                GraphicsTechnologySupportSourceKind.OfficialVendor, "AMD", DateTimeOffset.UtcNow)));

        var result = await service.DetectAsync(GameId.New(), _root, "Dune: Awakening", CancellationToken.None);
        var fsr = Find(result, GraphicsTechnology.FSR);

        Assert.Equal(GraphicsSupportStatus.Supported, fsr.SupportStatus);
        Assert.Equal(GraphicsRuntimeStatus.Unknown, fsr.RuntimeStatus);
        Assert.Equal(GraphicsTechnologySupportSourceKind.OfficialVendor, Assert.Single(fsr.SupportEvidenceList).SourceKind);
        Assert.Equal("Disponible", fsr.RuntimeStatusLabel);
    }

    [Fact]
    public async Task Local_runtime_evidence_has_priority_over_official_support()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "ffx_fsr3_api_dx12_x64.dll"), [1]);
        var service = new LocalGraphicsTechnologyDetectionService(new FakeSupportSource(
            new GraphicsTechnologySupportEvidence(GameId.New(), GraphicsTechnology.FSR, GraphicsSupportStatus.Supported,
                GraphicsTechnologySupportSourceKind.OfficialVendor, "AMD", DateTimeOffset.UtcNow)));

        var result = await service.DetectAsync(GameId.New(), _root, "Enshrouded", CancellationToken.None);
        var fsr = Find(result, GraphicsTechnology.FSR);

        Assert.Equal(GraphicsSupportStatus.Supported, fsr.SupportStatus);
        Assert.Equal(GraphicsRuntimeStatus.Present, fsr.RuntimeStatus);
        Assert.Equal(GraphicsTechnologySupportSourceKind.LocalEvidence, Assert.Single(fsr.SupportEvidenceList).SourceKind);
    }

    [Fact]
    public async Task Official_frame_generation_support_has_specific_label_but_unknown_runtime()
    {
        Directory.CreateDirectory(_root);
        var service = new LocalGraphicsTechnologyDetectionService(new FakeSupportSource(
            new GraphicsTechnologySupportEvidence(GameId.New(), GraphicsTechnology.FrameGeneration, GraphicsSupportStatus.Supported,
                GraphicsTechnologySupportSourceKind.OfficialVendor, "AMD", DateTimeOffset.UtcNow)));

        var result = await service.DetectAsync(GameId.New(), _root, "Dune: Awakening", CancellationToken.None);
        var frameGeneration = Find(result, GraphicsTechnology.FrameGeneration);

        Assert.Equal("FSR Frame Generation", frameGeneration.TechnologyLabel);
        Assert.Equal(GraphicsRuntimeStatus.Unknown, frameGeneration.RuntimeStatus);
        Assert.Equal(GraphicsSupportStatus.Supported, frameGeneration.SupportStatus);
    }

    private async Task<IReadOnlyList<GraphicsTechnologyObservation>> DetectAsync() =>
        await new LocalGraphicsTechnologyDetectionService().DetectAsync(GameId.New(), _root, CancellationToken.None);

    private sealed class FakeSupportSource(params GraphicsTechnologySupportEvidence[] values) : IGraphicsTechnologySupportSource
    {
        public Task<IReadOnlyList<GraphicsTechnologySupportEvidence>> GetSupportAsync(string gameTitle, GameId gameId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GraphicsTechnologySupportEvidence>>(values.Select(x => x with { GameId = gameId }).ToArray());
    }

    private static GraphicsTechnologyObservation Find(IEnumerable<GraphicsTechnologyObservation> values, GraphicsTechnology technology) =>
        Assert.Single(values, x => x.Technology == technology);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
