using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;

namespace PlayStead.Core.Tests.LocalEnvironment;

public sealed class ModRuntimeDetectionContributorTests
{
    [Fact]
    public async Task BepInEx_requires_framework_evidence_and_reports_plugins()
    {
        var root = Root();
        Directory.CreateDirectory(Path.Combine(root, "BepInEx", "core"));
        Directory.CreateDirectory(Path.Combine(root, "BepInEx", "plugins"));
        await File.WriteAllTextAsync(Path.Combine(root, "BepInEx", "version.txt"), "6.0.0");
        var entries = await Detect(root);
        var framework = Assert.Single(entries, x => x.Metadata!["ComponentKind"] == "BepInEx" && x.Metadata["IsRuntimeComponent"] == "true");
        Assert.Equal("6.0.0", framework.Metadata!["Version"]);
        Assert.Contains(entries, x => x.Metadata!["IsModDirectory"] == "true");
        Cleanup(root);
    }

    [Fact]
    public async Task BepInEx_directory_alone_is_not_enough()
    {
        var root = Root();
        Directory.CreateDirectory(Path.Combine(root, "BepInEx"));
        Assert.Empty(await Detect(root));
        Cleanup(root);
    }

    [Fact]
    public async Task Ue4Ss_requires_dll_and_known_companion()
    {
        var root = Root();
        var dir = Path.Combine(root, "UE4SS");
        Directory.CreateDirectory(Path.Combine(dir, "Mods"));
        await File.WriteAllTextAsync(Path.Combine(dir, "UE4SS.dll"), "fixture");
        await File.WriteAllTextAsync(Path.Combine(dir, "UE4SS-settings.ini"), "[Settings]");
        var entries = await Detect(root);
        Assert.Contains(entries, x => x.Metadata!["ComponentKind"] == "UE4SS" && x.Metadata["IsRuntimeComponent"] == "true");
        Cleanup(root);
    }

    [Fact]
    public async Task Ue4Ss_mods_directory_without_dll_is_rejected()
    {
        var root = Root();
        Directory.CreateDirectory(Path.Combine(root, "UE4SS", "Mods"));
        Assert.Empty(await Detect(root));
        Cleanup(root);
    }

    [Fact]
    public async Task ReShade_requires_ini_and_companion_evidence()
    {
        var root = Root();
        Directory.CreateDirectory(Path.Combine(root, "reshade-shaders"));
        await File.WriteAllTextAsync(Path.Combine(root, "ReShade.ini"), "[GENERAL]");
        await File.WriteAllTextAsync(Path.Combine(root, "dxgi.dll"), "fixture");
        var entries = await Detect(root);
        Assert.Contains(entries, x => x.Metadata!["ComponentKind"] == "ReShade" && x.Metadata["IsRuntimeComponent"] == "true");
        Cleanup(root);
    }

    [Fact]
    public async Task ReShade_dll_alone_is_rejected()
    {
        var root = Root();
        await File.WriteAllTextAsync(Path.Combine(root, "dxgi.dll"), "fixture");
        Assert.Empty(await Detect(root));
        Cleanup(root);
    }

    [Fact]
    public async Task Customization_state_is_conservative_and_strong_vanilla_proof_is_explicit()
    {
        var root = Root();
        var installation = Installation(root);
        var store = new MemoryStore();
        var runtimeService = new LocalEnvironmentDiscoveryService([new ModRuntimeDetectionContributor()], store, (id, _) => Task.FromResult<GameInstallation?>(installation));
        await runtimeService.RefreshEnvironmentAsync(installation.Id, CancellationToken.None);
        Assert.Equal(LocalEnvironmentCustomizationState.Indeterminate, await runtimeService.GetCustomizationStateAsync(installation.Id, CancellationToken.None));

        var vanilla = new LocalEnvironmentEntry(Guid.NewGuid(), installation.Id, LocalEnvironmentEntryKind.Mod, root, LocalEnvironmentPathKind.Directory, LocalEnvironmentConfidence.Confirmed, LocalEnvironmentSource.Catalog, false, DateTimeOffset.UtcNow, Metadata: new Dictionary<string, string> { ["VanillaProof"] = "Confirmed" });
        store.Detected[installation.Id] = [vanilla];
        Assert.Equal(LocalEnvironmentCustomizationState.VanillaConfirmed, await runtimeService.GetCustomizationStateAsync(installation.Id, CancellationToken.None));
        Cleanup(root);
    }

    [Fact]
    public async Task Cancellation_is_observed()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ModRuntimeDetectionContributor().DiscoverAsync(Installation(Root()), source.Token));
    }

    private static async Task<IReadOnlyList<LocalEnvironmentEntry>> Detect(string root)
    {
        var installation = Installation(root);
        return await new ModRuntimeDetectionContributor().DiscoverAsync(installation, CancellationToken.None);
    }

    private static GameInstallation Installation(string root) => new(InstallationId.New(), GameId.New(), ProviderKind.Manual, "fixture", root, null, true, true, DateTimeOffset.UtcNow);
    private static string Root() { var root = Path.Combine(Path.GetTempPath(), "PlayStead.ModRuntime-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); return root; }
    private static void Cleanup(string root) { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private sealed class MemoryStore : ILocalEnvironmentStore
    {
        public Dictionary<InstallationId, IReadOnlyList<LocalEnvironmentEntry>> Detected { get; } = [];
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetEntriesAsync(InstallationId id, CancellationToken token) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>(Detected.GetValueOrDefault(id, []));
        public Task ReplaceDetectedAsync(InstallationId id, IReadOnlyCollection<LocalEnvironmentEntry> entries, CancellationToken token) { Detected[id] = entries.ToArray(); return Task.CompletedTask; }
        public Task UpsertUserOverrideAsync(LocalEnvironmentEntry entry, CancellationToken token) => Task.CompletedTask;
        public Task RemoveUserOverrideAsync(InstallationId id, LocalEnvironmentEntryKind kind, string path, CancellationToken token) => Task.CompletedTask;
    }
}
