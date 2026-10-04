using PlayStead.Core.Library;
using PlayStead.Core.ProviderInstallUpdate;
using PlayStead.Providers.Epic;

namespace PlayStead.Providers.Tests.Epic;

public sealed class EpicLocalInstallUpdateStateSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.EpicUpdate-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task No_update_artifacts_is_current()
    {
        Directory.CreateDirectory(_root);
        var state = await ReadAsync(ProviderKind.Epic);
        Assert.Equal(ProviderInstallUpdateStatus.UpToDate, state.Status);
    }

    [Fact]
    public async Task Pending_and_resume_markers_are_active_update()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".egstore", "Pending"));
        Directory.CreateDirectory(Path.Combine(_root, ".egstore", "bps", "m"));
        File.WriteAllText(Path.Combine(_root, ".egstore", "Pending", "hll.mancpn"), "pending");
        File.WriteAllText(Path.Combine(_root, ".egstore", "bps", "m", "$resumeData"), "resume");
        var state = await ReadAsync(ProviderKind.Epic);
        Assert.Equal(ProviderInstallUpdateStatus.Downloading, state.Status);
    }

    [Fact]
    public async Task Non_epic_installation_is_ignored()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".egstore", "Pending"));
        File.WriteAllText(Path.Combine(_root, ".egstore", "Pending", "hll.mancpn"), "pending");
        var state = await new EpicLocalInstallUpdateStateSource().GetAsync(
            [Installation(ProviderKind.Steam)], CancellationToken.None);
        Assert.Empty(state);
    }

    private async Task<ProviderInstallUpdateState> ReadAsync(ProviderKind provider)
    {
        var state = await new EpicLocalInstallUpdateStateSource().GetAsync(
            [Installation(provider)], CancellationToken.None);
        return Assert.Single(state);
    }

    private GameInstallation Installation(ProviderKind provider) =>
        new(InstallationId.New(), GameId.New(), provider, "hll", _root, 1, true, true, DateTimeOffset.UtcNow,
            LaunchMetadata: provider == ProviderKind.Epic
                ? new ProviderLaunchMetadata(ProviderKind.Epic, new Dictionary<string, string>
                {
                    ["AppName"] = "hll-app"
                })
                : null);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
