using System.Net;
using System.Text;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Providers.Gog;

namespace PlayStead.Providers.Tests.Gog;

public sealed class GogGamesV2MetadataTests
{
    [Fact]
    public void Parses_real_contract_paths_and_sanitizes_description()
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Fixture("Gog/GogV2CrysisFr.json")));
        var details = GogGamesV2Details.Parse(document.RootElement, "1103900211");

        Assert.NotNull(details);
        Assert.Equal("1103900211", details.ProductId);
        Assert.Equal("Crysis Remastered", details.Title);
        Assert.Equal(["Crytek"], details.Developers);
        Assert.Equal(["Crytek"], details.Publishers);
        Assert.Equal(new DateOnly(2021, 9, 17), details.ReleaseDate);
        Assert.Equal(["Tir"], details.Tags);
        Assert.True(details.SinglePlayer);
        Assert.Equal("Un jeu & test.", details.Description);
    }

    [Fact]
    public void Witcher_title_is_diagnostic_only_and_modes_remain_unknown()
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Fixture("Gog/GogV2WitcherFr.json")));
        var details = GogGamesV2Details.Parse(document.RootElement, "1495134320");

        Assert.Equal("The Witcher 3: Wild Hunt — Remastered", details!.Title);
        Assert.DoesNotContain("<", details.Description!);
    }

    [Fact]
    public async Task Source_maps_exact_gog_installations_and_preserves_unknown_capabilities()
    {
        var game = GameId.New();
        var source = new GogGamesV2GameMetadataSource(
            new StubClient(File.ReadAllText(Fixture("Gog/GogV2CrysisFr.json"))), new MemoryStore());
        var installation = new GameInstallation(InstallationId.New(), game, ProviderKind.Gog, "1103900211", "C:\\Games", null, true, true, DateTimeOffset.UtcNow);

        var patch = Assert.Single(await source.GetAsync(new LibrarySnapshot([], [installation]), CancellationToken.None));

        Assert.Equal(game, patch.GameId);
        Assert.Equal(ProviderKind.Gog, patch.Provider);
        Assert.Equal("1103900211", patch.ProviderGameId);
        Assert.True(patch.SinglePlayer.Value);
        Assert.Equal(ProviderFieldState.NotReported, patch.MultiPlayer.State);
        Assert.Equal(ProviderFieldState.NotReported, patch.OnlineCoop.State);
        Assert.Equal(ProviderFieldState.NotReported, patch.LocalCoop.State);
    }

    [Fact]
    public async Task Mismatched_product_identity_is_rejected()
    {
        var source = new GogGamesV2GameMetadataSource(new StubClient("{\"_embedded\":{\"product\":{\"id\":999}}}"), new MemoryStore());
        var installation = new GameInstallation(InstallationId.New(), GameId.New(), ProviderKind.Gog, "1103900211", "C:\\Games", null, true, true, DateTimeOffset.UtcNow);
        Assert.Empty(await source.GetAsync(new LibrarySnapshot([], [installation]), CancellationToken.None));
    }

    [Fact]
    public async Task Client_returns_null_for_404_and_propagates_cancellation()
    {
        var client = new GogGamesV2Client(new HttpClient(new Handler(HttpStatusCode.NotFound, "{\"message\":\"Product not found\"}")));
        Assert.Null(await client.GetAsync("1103900211", CancellationToken.None));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("1103900211", cancellation.Token));
    }

    private static string Fixture(string relative) => Path.Combine(AppContext.BaseDirectory, "Fixtures", relative.Replace('/', Path.DirectorySeparatorChar));

    private sealed class StubClient(string payload) : IGogGamesV2Client
    {
        public Task<GogGamesV2Details?> GetAsync(string productId, CancellationToken cancellationToken)
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            return Task.FromResult(GogGamesV2Details.Parse(document.RootElement, productId));
        }
    }

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/vnd.error+json") });
        }
    }

    private sealed class MemoryStore : IProviderGameMetadataStore
    {
        public Task<IReadOnlyList<ProviderGameMetadata>> GetAllAsync(CancellationToken _) => Task.FromResult<IReadOnlyList<ProviderGameMetadata>>([]);
        public Task<ProviderGameMetadata?> GetAsync(GameId _, ProviderKind __, CancellationToken ___) => Task.FromResult<ProviderGameMetadata?>(null);
        public Task UpsertAsync(ProviderGameMetadata _, CancellationToken __) => Task.CompletedTask;
    }
}
