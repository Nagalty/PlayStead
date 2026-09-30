using PlayStead.Core.Library;

namespace PlayStead.Core.Graphics;

public enum GraphicsTechnology
{
    DirectX12,
    DLSS,
    FSR,
    XeSS,
    FrameGeneration
}

public enum GraphicsSupportStatus
{
    Supported,
    Unsupported,
    Unknown
}

public enum GraphicsRuntimeStatus
{
    Present,
    Absent,
    Unknown
}

public enum GraphicsActivationStatus
{
    ActiveConfirmed,
    InactiveConfirmed,
    Unknown
}

public enum GraphicsEvidenceKind
{
    RuntimeFilePresent,
    RuntimeVersionInfo,
    KnownConfigValue,
    ProviderMetadata,
    GameSpecificRule
}

public enum GraphicsDetectionCoverage
{
    PartialKnownLocations,
    Unavailable
}

public enum GraphicsTechnologySupportSourceKind
{
    LocalEvidence,
    OfficialVendor
}

public sealed record GraphicsTechnologySupportEvidence(
    GameId GameId,
    GraphicsTechnology Technology,
    GraphicsSupportStatus SupportStatus,
    GraphicsTechnologySupportSourceKind SourceKind,
    string SourceName,
    DateTimeOffset ObservedAtUtc,
    string? MatchedTitle = null);

public interface IGraphicsTechnologySupportSource
{
    Task<IReadOnlyList<GraphicsTechnologySupportEvidence>> GetSupportAsync(
        string gameTitle,
        GameId gameId,
        CancellationToken cancellationToken);
}

public sealed record GraphicsTechnologyEvidence(
    GameId GameId,
    GraphicsTechnology Technology,
    GraphicsEvidenceKind Kind,
    string EvidencePath,
    DateTimeOffset ObservedAtUtc,
    double Confidence,
    string? Detail = null);

public sealed record GraphicsTechnologyObservation(
    GameId GameId,
    GraphicsTechnology Technology,
    GraphicsSupportStatus SupportStatus,
    GraphicsRuntimeStatus RuntimeStatus,
    GraphicsActivationStatus ActivationStatus,
    string? RuntimeVersion,
    IReadOnlyList<GraphicsTechnologyEvidence> Evidence,
    IReadOnlyList<string>? ScannedLocations = null,
    GraphicsDetectionCoverage DetectionCoverage = GraphicsDetectionCoverage.PartialKnownLocations,
    IReadOnlyList<GraphicsTechnologySupportEvidence>? SupportEvidence = null,
    string? DisplayTechnologyLabel = null)
{
    public IReadOnlyList<string> ScannedLocationList => ScannedLocations ?? [];
    public IReadOnlyList<GraphicsTechnologySupportEvidence> SupportEvidenceList => SupportEvidence ?? [];
    public string TechnologyLabel => DisplayTechnologyLabel ?? Technology switch
    {
        GraphicsTechnology.DLSS => "DLSS",
        GraphicsTechnology.FSR => "FSR",
        GraphicsTechnology.XeSS => "XeSS",
        GraphicsTechnology.FrameGeneration => Evidence.Any(e =>
            string.Equals(Path.GetFileName(e.EvidencePath), "nvngx_dlssg.dll", StringComparison.OrdinalIgnoreCase))
                ? "DLSS Frame Generation"
                : Evidence.Any(e => string.Equals(Path.GetFileName(e.EvidencePath), "ffx_fsr3_api_dx12_x64.dll", StringComparison.OrdinalIgnoreCase))
                    ? "FSR Frame Generation"
                    : "Frame Generation",
        GraphicsTechnology.DirectX12 => "DirectX 12",
        _ => Technology.ToString()
    };

    public string RuntimeStatusLabel => RuntimeStatus switch
    {
        GraphicsRuntimeStatus.Present => "Disponible",
        GraphicsRuntimeStatus.Absent => "Non disponible",
        GraphicsRuntimeStatus.Unknown when SupportStatus == GraphicsSupportStatus.Supported => "Disponible",
        _ => "État inconnu"
    };

    public string ActivationStatusLabel => ActivationStatus switch
    {
        GraphicsActivationStatus.ActiveConfirmed => "Actif",
        GraphicsActivationStatus.InactiveConfirmed => "Inactif",
        _ => "Activation inconnue"
    };
}

public interface IGraphicsTechnologyDetectionService
{
    Task<IReadOnlyList<GraphicsTechnologyObservation>> DetectAsync(
        GameId gameId,
        string? installPath,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GraphicsTechnologyObservation>> DetectAsync(
        GameId gameId,
        string? installPath,
        string? gameTitle,
        CancellationToken cancellationToken) => DetectAsync(gameId, installPath, cancellationToken);
}
