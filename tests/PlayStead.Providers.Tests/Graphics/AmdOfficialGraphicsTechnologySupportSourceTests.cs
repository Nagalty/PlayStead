using System.Net;
using System.Net.Http;
using System.Text;
using PlayStead.Core.Graphics;
using PlayStead.Core.Library;
using PlayStead.Providers.Graphics;

namespace PlayStead.Providers.Tests.Graphics;

public sealed class AmdOfficialGraphicsTechnologySupportSourceTests : IDisposable
{
    private readonly string _cachePath = Path.Combine(Path.GetTempPath(), "PlayStead-Amd-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public async Task Official_catalog_supports_fsr_and_frame_generation()
    {
        var source = CreateSource(OfficialHtml);

        var result = await source.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);

        Assert.Contains(result, x => x.Technology == GraphicsTechnology.FSR && x.SupportStatus == GraphicsSupportStatus.Supported);
        Assert.Contains(result, x => x.Technology == GraphicsTechnology.FrameGeneration && x.SupportStatus == GraphicsSupportStatus.Supported);
        Assert.All(result, x => Assert.Equal(GraphicsTechnologySupportSourceKind.OfficialVendor, x.SourceKind));
        Assert.All(result, x => Assert.Equal("AMD", x.SourceName));
    }

    [Fact]
    public async Task Exact_normalization_accepts_punctuation_but_missing_entry_stays_unknown()
    {
        var source = CreateSource(OfficialHtml);

        var normalized = await source.GetSupportAsync(" DUNE™:  AWAKENING ", GameId.New(), CancellationToken.None);
        var missing = await source.GetSupportAsync("Dune Awakening 2", GameId.New(), CancellationToken.None);

        Assert.NotEmpty(normalized);
        Assert.Empty(missing);
    }

    [Fact]
    public async Task Enshrouded_is_matched_from_the_official_fsr_section_without_hardcoding()
    {
        var source = CreateSource(OfficialHtml);

        var result = await source.GetSupportAsync("Enshrouded", GameId.New(), CancellationToken.None);

        Assert.Contains(result, x => x.Technology == GraphicsTechnology.FSR);
        Assert.DoesNotContain(result, x => x.Technology == GraphicsTechnology.FrameGeneration);
    }

    [Fact]
    public async Task Ambiguous_duplicate_catalog_entry_stays_unknown()
    {
        var source = CreateSource("""
            <h2>AMD FSR 3</h2>
            <table><tr><td>Example Game</td></tr><tr><td>Example Game</td></tr></table>
            """);

        var result = await source.GetSupportAsync("Example Game", GameId.New(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Fresh_cache_is_used_without_network()
    {
        var handler = new CountingHandler(OfficialHtml);
        var source = CreateSource(handler, DateTimeOffset.Parse("2026-09-30T12:00:00Z"));
        await source.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);

        var offline = new AmdOfficialGraphicsTechnologySupportSource(
            new HttpClient(new ThrowingHandler()),
            _cachePath,
            TimeProvider.System,
            DateTimeOffset.Parse("2026-10-01T12:00:00Z"));
        var result = await offline.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);

        Assert.NotEmpty(result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Stale_cache_is_refreshed_from_the_official_source()
    {
        var initial = CreateSource(OfficialHtml, DateTimeOffset.Parse("2026-09-01T12:00:00Z"));
        await initial.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);
        var handler = new CountingHandler("<h2>AMD FSR 3</h2><table><tr><td>Enshrouded</td></tr></table>");
        var refreshed = new AmdOfficialGraphicsTechnologySupportSource(
            new HttpClient(handler),
            _cachePath,
            TimeProvider.System,
            DateTimeOffset.Parse("2026-09-30T12:00:00Z"));

        var result = await refreshed.GetSupportAsync("Enshrouded", GameId.New(), CancellationToken.None);

        Assert.Contains(result, x => x.Technology == GraphicsTechnology.FSR);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Failed_network_uses_stale_cache_and_no_cache_stays_unknown()
    {
        var source = CreateSource(OfficialHtml, DateTimeOffset.Parse("2026-09-01T12:00:00Z"));
        await source.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);

        var offline = new AmdOfficialGraphicsTechnologySupportSource(
            new HttpClient(new ThrowingHandler()),
            _cachePath,
            TimeProvider.System,
            DateTimeOffset.Parse("2026-09-30T12:00:00Z"));
        var cached = await offline.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);
        var missing = await offline.GetSupportAsync("Unknown Game", GameId.New(), CancellationToken.None);

        Assert.NotEmpty(cached);
        Assert.Empty(missing);
    }

    [Fact]
    public async Task No_cache_and_network_failure_returns_unknown()
    {
        var source = new AmdOfficialGraphicsTechnologySupportSource(
            new HttpClient(new ThrowingHandler()),
            _cachePath,
            TimeProvider.System,
            DateTimeOffset.Parse("2026-09-30T12:00:00Z"));

        var result = await source.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Recent_official_catalog_snapshot_matches_dune_and_enshrouded()
    {
        var html = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "amd-supported-games-live-2026-09-30.html"));
        var source = CreateSource(html);

        var dune = await source.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);
        var enshrouded = await source.GetSupportAsync("Enshrouded", GameId.New(), CancellationToken.None);

        Assert.Contains(dune, x => x.Technology == GraphicsTechnology.FSR);
        Assert.Contains(dune, x => x.Technology == GraphicsTechnology.FrameGeneration);
        Assert.Contains(enshrouded, x => x.Technology == GraphicsTechnology.FSR);
    }

    [Fact]
    public async Task Legacy_cache_without_version_is_invalidated_and_refreshed()
    {
        await WriteRawCacheAsync("{\"FetchedAtUtc\":\"2026-09-30T11:00:00Z\",\"Entries\":[]}");
        var handler = new CountingHandler(OfficialHtml);
        var source = CreateSource(handler, DateTimeOffset.Parse("2026-09-30T12:00:00Z"));

        var result = await source.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);

        Assert.NotEmpty(result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Parser_version_mismatch_is_invalidated_and_refreshed()
    {
        await WriteRawCacheAsync("{\"FetchedAtUtc\":\"2026-09-30T11:00:00Z\",\"Entries\":[],\"SchemaVersion\":2,\"ParserVersion\":1}");
        var handler = new CountingHandler(OfficialHtml);
        var source = CreateSource(handler, DateTimeOffset.Parse("2026-09-30T12:00:00Z"));

        var result = await source.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);

        Assert.Contains(result, x => x.Technology == GraphicsTechnology.FSR);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Refreshed_cache_contains_dune_and_enshrouded_for_subsequent_offline_reads()
    {
        var handler = new CountingHandler(OfficialHtml);
        var source = CreateSource(handler, DateTimeOffset.Parse("2026-09-30T12:00:00Z"));
        await source.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);

        var offline = new AmdOfficialGraphicsTechnologySupportSource(
            new HttpClient(new ThrowingHandler()),
            _cachePath,
            TimeProvider.System,
            DateTimeOffset.Parse("2026-10-01T12:00:00Z"));
        var dune = await offline.GetSupportAsync("Dune: Awakening", GameId.New(), CancellationToken.None);
        var enshrouded = await offline.GetSupportAsync("Enshrouded", GameId.New(), CancellationToken.None);

        Assert.Contains(dune, x => x.Technology == GraphicsTechnology.FSR);
        Assert.Contains(enshrouded, x => x.Technology == GraphicsTechnology.FSR);
        Assert.Equal(1, handler.RequestCount);
    }

    private async Task WriteRawCacheAsync(string json)
    {
        await File.WriteAllTextAsync(_cachePath, json);
    }

    private AmdOfficialGraphicsTechnologySupportSource CreateSource(string html, DateTimeOffset? now = null) =>
        CreateSource(new CountingHandler(html), now);

    private AmdOfficialGraphicsTechnologySupportSource CreateSource(HttpMessageHandler handler, DateTimeOffset? now = null) =>
        new(
            new HttpClient(handler),
            _cachePath,
            TimeProvider.System,
            now ?? DateTimeOffset.Parse("2026-09-30T12:00:00Z"));

    public void Dispose()
    {
        if (File.Exists(_cachePath))
            File.Delete(_cachePath);
    }

    private sealed class CountingHandler(string html) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"));
    }

    private const string OfficialHtml = """
        <html><body>
        <h2>AMD FSR Frame Generation</h2>
        <table><tr><td>Dune: Awakening</td></tr></table>
        <h2>AMD FSR 3</h2>
        <table><tr><td>Enshrouded</td></tr><tr><td>Dune: Awakening</td></tr></table>
        </body></html>
        """;
}
