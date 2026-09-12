using PlayStead.Core.Steam;

namespace PlayStead.Providers.Steam.Evidence;

public interface ISteamLocalEvidenceSource
{
    Task<IReadOnlyList<SteamLocalEvidence>> ScanAsync(
        CancellationToken cancellationToken);
}
