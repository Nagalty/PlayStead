using PlayStead.Core.Library;
using PlayStead.Core.Modding;

namespace PlayStead.Providers.Steam;

public sealed class SteamWorkshopModEvidenceDetector : IModEvidenceDetector
{
    public const string Id = "steam-workshop-content";
    public string DetectorId => Id;

    public Task<IReadOnlyList<ModEvidence>> DetectAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installation);
        cancellationToken.ThrowIfCancellationRequested();

        if (installation.Provider != ProviderKind.Steam ||
            !uint.TryParse(installation.ExternalId, out var appId))
        {
            return Task.FromResult<IReadOnlyList<ModEvidence>>([]);
        }

        string? steamApps;
        try
        {
            steamApps = FindSteamAppsDirectory(installation.InstallPath);
        }
        catch (ArgumentException)
        {
            return Task.FromResult<IReadOnlyList<ModEvidence>>([]);
        }
        if (steamApps is null)
            return Task.FromResult<IReadOnlyList<ModEvidence>>([]);

        var contentRoot = Path.Combine(steamApps, "workshop", "content", appId.ToString());
        var present = Directory.Exists(contentRoot) &&
            Directory.EnumerateDirectories(contentRoot).Any();
        if (!present)
            return Task.FromResult<IReadOnlyList<ModEvidence>>([]);

        IReadOnlyList<ModEvidence> result = [new ModEvidence(
            installation.GameId,
            installation.Provider,
            DetectorId,
            ModEvidenceKind.WorkshopContentPresent,
            ModDetectionState.PossiblyModded,
            DateTimeOffset.UtcNow,
            $"Workshop content present for AppId {appId}.")];
        return Task.FromResult(result);
    }

    private static string? FindSteamAppsDirectory(string installPath)
    {
        var current = new DirectoryInfo(Path.GetFullPath(installPath));
        while (current is not null)
        {
            if (string.Equals(current.Name, "common", StringComparison.OrdinalIgnoreCase) &&
                current.Parent?.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase) == true)
                return current.Parent.FullName;
            current = current.Parent;
        }
        return null;
    }
}
