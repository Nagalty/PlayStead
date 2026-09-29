using PlayStead.Core.Library;
using PlayStead.Core.Modding;

namespace PlayStead.Providers.Steam;

/// <summary>
/// Bounded, game-specific mod detectors.  A loader by itself is only a
/// possible signal; confirmation requires a known plugin location and a
/// concrete plugin file.
/// </summary>
public sealed class SteamGameSpecificModEvidenceDetector : IModEvidenceDetector
{
    public const string Id = "steam-game-specific";
    private const string Fallout4AppId = "377160";
    private const string ReadyOrNotAppId = "1144200";

    public string DetectorId => Id;

    public Task<IReadOnlyList<ModEvidence>> DetectAsync(
        GameInstallation installation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installation);
        cancellationToken.ThrowIfCancellationRequested();

        if (installation.Provider != ProviderKind.Steam)
            return Task.FromResult<IReadOnlyList<ModEvidence>>([]);

        try
        {
            if (string.Equals(installation.ExternalId, ReadyOrNotAppId, StringComparison.Ordinal))
            {
                var modsPath = Path.Combine(installation.InstallPath, "ReadyOrNot", "Content", "Paks", "~mods");
                var modPackages = Directory.Exists(modsPath)
                    ? Directory.EnumerateFiles(modsPath, "*.pak", SearchOption.TopDirectoryOnly).ToArray()
                    : [];

                if (modPackages.Length == 0)
                    return Task.FromResult<IReadOnlyList<ModEvidence>>([]);

                return Task.FromResult<IReadOnlyList<ModEvidence>>([
                    new ModEvidence(
                        installation.GameId,
                        installation.Provider,
                        Id,
                        ModEvidenceKind.ModFilesDetected,
                        ModDetectionState.ConfirmedModded,
                        DateTimeOffset.UtcNow,
                        "Ready or Not package detected in the reserved Content/Paks/~mods location.")
                ]);
            }

            if (!string.Equals(installation.ExternalId, Fallout4AppId, StringComparison.Ordinal))
                return Task.FromResult<IReadOnlyList<ModEvidence>>([]);

            var loaderPresent = File.Exists(Path.Combine(installation.InstallPath, "f4se_loader.exe")) ||
                Directory.EnumerateFiles(installation.InstallPath, "f4se*.dll", SearchOption.TopDirectoryOnly).Any();
            if (!loaderPresent)
                return Task.FromResult<IReadOnlyList<ModEvidence>>([]);

            var evidence = new List<ModEvidence>
            {
                new(
                    installation.GameId,
                    installation.Provider,
                    Id,
                    ModEvidenceKind.ModLoaderPresent,
                    ModDetectionState.PossiblyModded,
                    DateTimeOffset.UtcNow,
                    "Fallout 4 F4SE loader present; a plugin is required for confirmation.")
            };

            var pluginDirectory = Path.Combine(installation.InstallPath, "Data", "F4SE", "Plugins");
            var plugins = Directory.Exists(pluginDirectory)
                ? Directory.EnumerateFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly).ToArray()
                : [];
            if (plugins.Length > 0)
            {
                evidence.Add(new(
                    installation.GameId,
                    installation.Provider,
                    Id,
                    ModEvidenceKind.ModFilesDetected,
                    ModDetectionState.ConfirmedModded,
                    DateTimeOffset.UtcNow,
                    $"F4SE plugin detected in {Path.Combine("Data", "F4SE", "Plugins")}."));
            }

            return Task.FromResult<IReadOnlyList<ModEvidence>>(evidence);
        }
        catch (ArgumentException)
        {
            return Task.FromResult<IReadOnlyList<ModEvidence>>([]);
        }
        catch (IOException)
        {
            return Task.FromResult<IReadOnlyList<ModEvidence>>([]);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult<IReadOnlyList<ModEvidence>>([]);
        }
    }
}
