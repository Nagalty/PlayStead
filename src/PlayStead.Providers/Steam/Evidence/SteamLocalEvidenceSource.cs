using PlayStead.Core.Steam;

namespace PlayStead.Providers.Steam.Evidence;

public sealed class SteamLocalEvidenceSource :
    ISteamLocalEvidenceSource
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLibraryFoldersReader _foldersReader;
    private readonly SteamLocalEvidenceReader _evidenceReader;

    public SteamLocalEvidenceSource(
        WindowsSteamRootLocator rootLocator,
        SteamLibraryFoldersReader foldersReader,
        SteamLocalEvidenceReader evidenceReader)
    {
        ArgumentNullException.ThrowIfNull(rootLocator);
        ArgumentNullException.ThrowIfNull(foldersReader);
        ArgumentNullException.ThrowIfNull(evidenceReader);

        _rootLocator = rootLocator;
        _foldersReader = foldersReader;
        _evidenceReader = evidenceReader;
    }

    public Task<IReadOnlyList<SteamLocalEvidence>> ScanAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var steamRoot = _rootLocator.TryLocate();

        if (steamRoot is null)
        {
            return Task.FromResult<IReadOnlyList<SteamLocalEvidence>>(
                Array.Empty<SteamLocalEvidence>());
        }

        var observedAtUtc = DateTimeOffset.UtcNow;
        var found = new List<SteamLocalEvidence>();

        foreach (var libraryRoot in _foldersReader.Read(steamRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var steamApps = Path.Combine(
                libraryRoot,
                "steamapps");

            foreach (var manifestPath in Directory.EnumerateFiles(
                         steamApps,
                         "appmanifest_*.acf",
                         SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                found.Add(
                    _evidenceReader.Read(
                        manifestPath,
                        observedAtUtc));
            }
        }

        IReadOnlyList<SteamLocalEvidence> ordered = found
            .OrderBy(
                item => item.AppId,
                StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(ordered);
    }
}
