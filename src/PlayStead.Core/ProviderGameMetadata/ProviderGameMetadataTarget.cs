using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.ProviderGameMetadata;

public enum ProviderGameMetadataTargetOrigin
{
    Native = 0,
    ManualBridge = 1
}

public sealed record ProviderGameMetadataTarget(
    GameId TargetGameId,
    ProviderKind SourceProvider,
    string SourceExternalId,
    ProviderGameMetadataTargetOrigin Origin);

public interface IProviderGameMetadataTargetResolver
{
    Task<IReadOnlyList<ProviderGameMetadataTarget>> ResolveAsync(
        LibrarySnapshot snapshot,
        CancellationToken cancellationToken);

    Task<bool> IsCurrentAsync(
        ProviderGameMetadataTarget target,
        CancellationToken cancellationToken) => Task.FromResult(true);
}

public sealed class ProviderGameMetadataTargetResolver : IProviderGameMetadataTargetResolver
{
    private readonly IManualMetadataLinkStore _manualMetadataLinks;

    public ProviderGameMetadataTargetResolver(IManualMetadataLinkStore manualMetadataLinks) =>
        _manualMetadataLinks = manualMetadataLinks ?? throw new ArgumentNullException(nameof(manualMetadataLinks));

    public async Task<IReadOnlyList<ProviderGameMetadataTarget>> ResolveAsync(
        LibrarySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var targets = new Dictionary<(GameId GameId, ProviderKind Provider, string ExternalId), ProviderGameMetadataTarget>();
        var manualLinks = new Dictionary<GameId, ManualMetadataLink?>();
        foreach (var installation in snapshot.Installations.Where(x => x.IsPresent))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (installation.Provider == ProviderKind.Steam)
            {
                if (IsNumericAppId(installation.ExternalId))
                    AddTarget(new(installation.GameId, ProviderKind.Steam, installation.ExternalId, ProviderGameMetadataTargetOrigin.Native));
                continue;
            }

            if (installation.Provider != ProviderKind.Manual)
                continue;

            if (!manualLinks.TryGetValue(installation.GameId, out var link))
            {
                link = await _manualMetadataLinks.GetAsync(installation.GameId, cancellationToken).ConfigureAwait(false);
                manualLinks[installation.GameId] = link;
            }

            if (link?.MediaSource is { Provider: ProviderKind.Steam } media && IsNumericAppId(media.ExternalId))
                AddTarget(new(installation.GameId, ProviderKind.Steam, media.ExternalId, ProviderGameMetadataTargetOrigin.ManualBridge));
        }

        return targets
            .Values
            .OrderBy(x => x.TargetGameId.Value)
            .ThenBy(x => x.SourceProvider)
            .ThenBy(x => x.SourceExternalId, StringComparer.Ordinal)
            .ThenBy(x => x.Origin)
            .ToArray();

        void AddTarget(ProviderGameMetadataTarget target)
        {
            var key = (target.TargetGameId, target.SourceProvider, target.SourceExternalId);
            if (!targets.TryGetValue(key, out var existing) ||
                existing.Origin != ProviderGameMetadataTargetOrigin.Native && target.Origin == ProviderGameMetadataTargetOrigin.Native)
                targets[key] = target;
        }
    }

    public async Task<bool> IsCurrentAsync(
        ProviderGameMetadataTarget target,
        CancellationToken cancellationToken)
    {
        if (target.Origin != ProviderGameMetadataTargetOrigin.ManualBridge)
            return true;

        var link = await _manualMetadataLinks.GetAsync(target.TargetGameId, cancellationToken).ConfigureAwait(false);
        return link?.MediaSource is { Provider: ProviderKind.Steam } media &&
               string.Equals(media.ExternalId, target.SourceExternalId, StringComparison.Ordinal);
    }

    private static bool IsNumericAppId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && uint.TryParse(value, out _);
}
