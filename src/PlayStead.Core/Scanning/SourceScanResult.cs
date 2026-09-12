using PlayStead.Core.Library;

namespace PlayStead.Core.Scanning;

public sealed record SourceScanResult(
    ProviderKind Provider,
    bool IsComplete,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<DiscoveredInstallation> Installations,
    IReadOnlyList<string> Warnings,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static SourceScanResult Success(
        ProviderKind provider,
        DateTimeOffset observedAtUtc,
        IReadOnlyList<DiscoveredInstallation> installations,
        IReadOnlyList<string>? warnings = null) =>
        new(
            provider,
            IsComplete: true,
            observedAtUtc,
            installations,
            warnings ?? Array.Empty<string>(),
            ErrorCode: null,
            ErrorMessage: null);

    public static SourceScanResult Failure(
        ProviderKind provider,
        DateTimeOffset observedAtUtc,
        string code,
        string message) =>
        new(
            provider,
            IsComplete: false,
            observedAtUtc,
            Array.Empty<DiscoveredInstallation>(),
            Array.Empty<string>(),
            code,
            message);
}
