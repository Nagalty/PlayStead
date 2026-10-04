using System.Text.Json;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.Providers.Epic;

/// <summary>Reads the small, local Epic install/update markers without using Epic services.</summary>
public sealed class EpicLocalInstallUpdateStateSource : IProviderInstallUpdateStateSource
{
    private readonly TimeProvider _timeProvider;

    public EpicLocalInstallUpdateStateSource(TimeProvider? timeProvider = null) =>
        _timeProvider = timeProvider ?? TimeProvider.System;

    public ProviderKind Provider => ProviderKind.Epic;

    public Task<IReadOnlyList<ProviderInstallUpdateState>> GetAsync(
        IReadOnlyCollection<GameInstallation> installations,
        CancellationToken cancellationToken)
    {
        var observedAt = _timeProvider.GetUtcNow();
        var result = new List<ProviderInstallUpdateState>();
        foreach (var installation in installations.Where(value =>
                     value.Provider == ProviderKind.Epic && value.IsPresent))
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(Read(installation, observedAt));
        }

        return Task.FromResult<IReadOnlyList<ProviderInstallUpdateState>>(
            result.OrderBy(value => value.ProviderGameId, StringComparer.Ordinal).ToArray());
    }

    private static ProviderInstallUpdateState Read(
        GameInstallation installation,
        DateTimeOffset observedAt)
    {
        var root = installation.InstallPath;
        var pending = Path.Combine(root, ".egstore", "Pending");
        var bps = Path.Combine(root, ".egstore", "bps");
        var mainManifest = FindInstalledManifest(installation.LaunchMetadata?["AppName"]);
        var incomplete = ReadIncompleteInstall(mainManifest);
        var pendingFiles = Directory.Exists(pending) &&
                           Directory.EnumerateFiles(pending, "*", SearchOption.TopDirectoryOnly).Any();
        var resumeData = File.Exists(Path.Combine(bps, "m", "$resumeData"));
        var payloadFiles = Directory.Exists(Path.Combine(bps, "f")) &&
                           Directory.EnumerateFiles(Path.Combine(bps, "f"), "*", SearchOption.TopDirectoryOnly).Any();
        var downloadState = HasMatchingUpdateState(installation, root);

        if (incomplete)
            return State(installation, ProviderInstallUpdateStatus.Staging, observedAt);

        if (pendingFiles || resumeData || payloadFiles || downloadState)
            return State(installation, ProviderInstallUpdateStatus.Downloading, observedAt);

        return State(installation, ProviderInstallUpdateStatus.UpToDate, observedAt);
    }

    private static ProviderInstallUpdateState State(
        GameInstallation installation,
        ProviderInstallUpdateStatus status,
        DateTimeOffset observedAt) =>
        new(
            installation.GameId,
            ProviderKind.Epic,
            installation.ExternalId,
            status == ProviderInstallUpdateStatus.UpToDate ? "current" : null,
            status == ProviderInstallUpdateStatus.UpToDate ? "current" : "pending",
            status,
            null,
            null,
            null,
            null,
            null,
            null,
            observedAt);

    private static bool ReadIncompleteInstall(string? manifestPath)
    {
        if (!File.Exists(manifestPath))
            return false;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return document.RootElement.TryGetProperty("bIsIncompleteInstall", out var value) &&
                   value.ValueKind == JsonValueKind.True;
        }
        catch (JsonException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static string? FindInstalledManifest(string? appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            return null;
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(directory))
            return null;
        foreach (var file in Directory.EnumerateFiles(directory, "*.item"))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                if (document.RootElement.TryGetProperty("AppName", out var value) &&
                    string.Equals(value.GetString(), appName, StringComparison.Ordinal))
                    return file;
            }
            catch (JsonException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }

    private static bool HasMatchingUpdateState(GameInstallation installation, string root)
    {
        var appName = installation.LaunchMetadata?["AppName"];
        if (string.IsNullOrWhiteSpace(appName))
            return false;
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "DownloadManager");
        if (!Directory.Exists(directory))
            return false;
        foreach (var file in Directory.EnumerateFiles(directory, "DownloadState_*.json"))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var entry in document.RootElement.EnumerateArray())
                {
                    if (!entry.TryGetProperty("AppId", out var appId) ||
                        !appId.TryGetProperty("AppName", out var value) ||
                        !string.Equals(value.GetString(), appName, StringComparison.Ordinal))
                        continue;
                    return entry.TryGetProperty("InstallType", out var installType) &&
                           string.Equals(installType.GetString(), "Update", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (JsonException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return false;
    }
}
