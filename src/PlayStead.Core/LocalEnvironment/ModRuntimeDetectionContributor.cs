using System.Diagnostics;
using PlayStead.Core.Library;

namespace PlayStead.Core.LocalEnvironment;

public sealed class ModRuntimeDetectionContributor : ILocalEnvironmentContributor
{
    public Task<IReadOnlyList<LocalEnvironmentEntry>> DiscoverAsync(GameInstallation installation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = installation.InstallRootPath ?? installation.InstallPath;
        var entries = new List<LocalEnvironmentEntry>();
        TryDetectBepInEx(root, installation.Id, entries, cancellationToken);
        TryDetectUe4Ss(root, installation.Id, entries, cancellationToken);
        TryDetectReShade(root, installation.Id, entries, cancellationToken);
        return Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>(entries);
    }

    private static void TryDetectBepInEx(string root, InstallationId installationId, ICollection<LocalEnvironmentEntry> entries, CancellationToken token)
    {
        var directory = Path.Combine(root, "BepInEx");
        if (!Directory.Exists(directory)) return;
        var evidence = new List<string>();
        if (Directory.Exists(Path.Combine(directory, "core"))) evidence.Add("BepInEx\\core");
        if (Directory.Exists(Path.Combine(directory, "plugins"))) evidence.Add("BepInEx\\plugins");
        if (Directory.Exists(Path.Combine(directory, "config"))) evidence.Add("BepInEx\\config");
        if (File.Exists(Path.Combine(root, "winhttp.dll"))) evidence.Add("winhttp.dll");
        if (File.Exists(Path.Combine(root, "doorstop_config.ini"))) evidence.Add("doorstop_config.ini");
        if (evidence.Count == 0) return;
        AddComponent(entries, installationId, directory, "BepInEx", "BepInEx", evidence, true, false, FindVersion(directory));
        if (Directory.Exists(Path.Combine(directory, "plugins"))) AddComponent(entries, installationId, Path.Combine(directory, "plugins"), "BepInEx", "BepInEx plugins", evidence, false, true, null);
        token.ThrowIfCancellationRequested();
    }

    private static void TryDetectUe4Ss(string root, InstallationId installationId, ICollection<LocalEnvironmentEntry> entries, CancellationToken token)
    {
        var directory = Path.Combine(root, "UE4SS");
        var dll = File.Exists(Path.Combine(directory, "UE4SS.dll")) ? Path.Combine(directory, "UE4SS.dll") : Path.Combine(root, "UE4SS.dll");
        var settings = File.Exists(Path.Combine(directory, "UE4SS-settings.ini"));
        var mods = Directory.Exists(Path.Combine(directory, "Mods"));
        if (!File.Exists(dll) || (!settings && !mods && !Directory.Exists(directory))) return;
        var evidence = new List<string> { Path.GetRelativePath(root, dll) };
        if (settings) evidence.Add("UE4SS\\UE4SS-settings.ini");
        if (mods) evidence.Add("UE4SS\\Mods");
        AddComponent(entries, installationId, directory, "UE4SS", "UE4SS", evidence, true, false, GetFileVersion(dll) ?? FindVersion(directory));
        if (mods) AddComponent(entries, installationId, Path.Combine(directory, "Mods"), "UE4SS", "UE4SS Mods", evidence, false, true, null);
        token.ThrowIfCancellationRequested();
    }

    private static void TryDetectReShade(string root, InstallationId installationId, ICollection<LocalEnvironmentEntry> entries, CancellationToken token)
    {
        var ini = new[] { "ReShade.ini", "ReShadePreset.ini" }.Select(x => Path.Combine(root, x)).FirstOrDefault(File.Exists);
        var shaders = Path.Combine(root, "reshade-shaders");
        var dll = new[] { "dxgi.dll", "d3d11.dll", "d3d12.dll", "opengl32.dll" }.Select(x => Path.Combine(root, x)).FirstOrDefault(File.Exists);
        if (ini is null || (!Directory.Exists(shaders) && dll is null)) return;
        var evidence = new List<string> { Path.GetFileName(ini) };
        if (Directory.Exists(shaders)) evidence.Add("reshade-shaders");
        if (dll is not null) evidence.Add(Path.GetFileName(dll));
        AddComponent(entries, installationId, root, "ReShade", "ReShade", evidence, true, false, GetFileVersion(dll));
        if (Directory.Exists(shaders)) AddComponent(entries, installationId, shaders, "ReShade", "ReShade shaders", evidence, false, true, null);
        token.ThrowIfCancellationRequested();
    }

    private static void AddComponent(ICollection<LocalEnvironmentEntry> entries, InstallationId installationId, string path, string kind, string name, IEnumerable<string> evidence, bool runtime, bool modDirectory, string? version)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ComponentKind"] = kind,
            ["ComponentName"] = name,
            ["DetectionEvidence"] = string.Join(";", evidence.Distinct(StringComparer.OrdinalIgnoreCase)),
            ["Framework"] = kind,
            ["IsRuntimeComponent"] = runtime ? "true" : "false",
            ["IsModDirectory"] = modDirectory ? "true" : "false"
        };
        if (!string.IsNullOrWhiteSpace(version)) metadata["Version"] = version;
        entries.Add(new LocalEnvironmentEntry(Guid.NewGuid(), installationId, LocalEnvironmentEntryKind.Mod, path, LocalEnvironmentPathKind.Directory, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.SystemDetection, false, DateTimeOffset.MinValue, null, metadata));
    }

    private static string? FindVersion(string directory)
    {
        var versionFile = Path.Combine(directory, "version.txt");
        if (!File.Exists(versionFile)) return null;
        try { return File.ReadAllText(versionFile).Trim().Length <= 64 ? File.ReadAllText(versionFile).Trim() : null; }
        catch (IOException) { return null; }
    }

    private static string? GetFileVersion(string? path)
    {
        if (path is null || !File.Exists(path)) return null;
        try { return FileVersionInfo.GetVersionInfo(path).FileVersion; }
        catch (Exception) { return null; }
    }
}
