using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamMediaProviderDiagnosticsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Local_hit_reports_one_local_provider_event()
    {
        var directory = Path.Combine(_root, "appcache", "librarycache");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, "1874880_library_600x900.jpg"), [1, 2, 3]);
        var diagnostics = new RecordingDiagnostics();
        var provider = CreateProvider(new StubTransport(null), diagnostics);

        var payload = await provider.ResolveAsync(Identity(), GameMediaAssetType.Cover, default);

        Assert.NotNull(payload);
        Assert.Collection(diagnostics.Events, item =>
            Assert.Equal(MediaResolutionEventKind.LocalProviderHit, item.Kind));
    }

    [Fact]
    public async Task Remote_result_reports_success_or_failure_once()
    {
        var successDiagnostics = new RecordingDiagnostics();
        var success = CreateProvider(new StubTransport(new GameMediaPayload(
            GameMediaAssetType.Cover, "steam-remote", "1874880", [1], "image/jpeg",
            new Uri("https://example.test/cover.jpg"))), successDiagnostics);

        var payload = await success.ResolveAsync(Identity(), GameMediaAssetType.Cover, default);

        Assert.NotNull(payload);
        Assert.Collection(successDiagnostics.Events, item =>
            Assert.Equal(MediaResolutionEventKind.RemoteProviderSuccess, item.Kind));

        var failureDiagnostics = new RecordingDiagnostics();
        var failure = CreateProvider(new StubTransport(null), failureDiagnostics);

        Assert.Null(await failure.ResolveAsync(Identity(), GameMediaAssetType.Cover, default));
        Assert.Collection(failureDiagnostics.Events, item =>
            Assert.Equal(MediaResolutionEventKind.RemoteProviderFailure, item.Kind));
    }

    private SteamMediaProvider CreateProvider(ISteamMediaTransport transport, IMediaDiagnostics diagnostics)
    {
        Directory.CreateDirectory(_root);
        return new SteamMediaProvider(new WindowsSteamRootLocator([_root]),
            new SteamLocalMediaLocator(), transport, diagnostics);
    }

    private static GameMediaIdentity Identity() =>
        new(ProviderKind.Steam, "1874880", "Arma Reforger");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class RecordingDiagnostics : IMediaDiagnostics
    {
        public List<MediaResolutionEvent> Events { get; } = [];
        public void Report(MediaResolutionEvent mediaEvent) => Events.Add(mediaEvent);
    }

    private sealed class StubTransport(GameMediaPayload? payload) : ISteamMediaTransport
    {
        public Task<GameMediaPayload?> TryDownloadAsync(string appId, GameMediaAssetType assetType,
            IReadOnlyList<Uri> candidates, CancellationToken cancellationToken) => Task.FromResult(payload);
    }
}
