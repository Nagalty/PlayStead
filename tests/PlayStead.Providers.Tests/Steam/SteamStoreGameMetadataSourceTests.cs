using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamStoreGameMetadataSourceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Enshrouded_fixture_maps_store_fields_and_coop()
    {
        var game = GameId.New();
        var source = CreateSource(_ => Fixture("SteamStore/1203620.json"));

        var patches = await source.GetAsync(Snapshot(game, "1203620"), CancellationToken.None);
        var patch = Assert.Single(patches);

        Assert.Equal(new DateOnly(2024, 1, 24), patch.ReleaseDate.Value);
        Assert.Equal(["Action", "Aventure", "Indépendant", "RPG", "Accès anticipé"], patch.Genres.Value);
        Assert.Equal(ProviderFieldState.Value, patch.ShortDescription.State);
        Assert.Contains("survie", patch.ShortDescription.Value!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ProviderFieldState.Value, patch.SinglePlayer.State);
        Assert.True(patch.SinglePlayer.Value);
        Assert.True(patch.MultiPlayer.Value);
        Assert.True(patch.OnlineCoop.Value);
        Assert.True(Apply(patch).SupportsCoop);
    }

    [Fact]
    public async Task Requests_french_localized_store_payload()
    {
        string? requestUri = null;
        var source = CreateSource(uri =>
        {
            requestUri = uri;
            return Fixture("SteamStore/1203620.json");
        });

        _ = await source.GetAsync(Snapshot(GameId.New(), "1203620"), CancellationToken.None);

        Assert.Contains("l=french", requestUri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cc=fr", requestUri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Root_entry_key_does_not_have_to_match_requested_app_id_when_payload_identity_matches()
    {
        var source = CreateSource(_ => Fixture("SteamStore/1203620.json"));

        var patch = Assert.Single(await source.GetAsync(Snapshot(GameId.New(), "1203620"), CancellationToken.None));

        Assert.Equal("1203620", patch.ProviderGameId);
    }

    [Fact]
    public async Task Mismatched_payload_identity_is_rejected()
    {
        var source = CreateSource(_ => Json("{\"4808980\":{\"success\":true,\"data\":{\"steam_appid\":999,\"name\":\"Wrong game\"}}}"));

        Assert.Empty(await source.GetAsync(Snapshot(GameId.New(), "1203620"), CancellationToken.None));
    }

    [Fact]
    public async Task French_category_descriptions_are_preserved_while_capabilities_use_observed_ids()
    {
        var source = CreateSource(_ => Fixture("SteamStore/1203620.json"));
        var patch = Assert.Single(await source.GetAsync(Snapshot(GameId.New(), "1203620"), CancellationToken.None));

        Assert.Equal(["Solo", "Multijoueur", "Coopération", "Coopération en ligne"], patch.Categories.Value);
        Assert.True(patch.SinglePlayer.Value);
        Assert.True(patch.MultiPlayer.Value);
        Assert.True(patch.OnlineCoop.Value);
    }

    [Fact]
    public async Task Generic_coop_category_does_not_infer_online_coop()
    {
        var source = CreateSource(_ => Json("{\"1\":{\"success\":true,\"data\":{\"steam_appid\":1,\"categories\":[{\"id\":\"9\",\"description\":\"Coopération\"}]}}}"));
        var patch = Assert.Single(await source.GetAsync(Snapshot(GameId.New(), "1"), CancellationToken.None));

        Assert.Equal(ProviderFieldState.NotReported, patch.OnlineCoop.State);
    }

    [Fact]
    public async Task Helldivers_fixture_maps_local_coop_and_multi_player()
    {
        var source = CreateSource(_ => Fixture("SteamStore/553850.json"));
        var patch = Assert.Single(await source.GetAsync(Snapshot(GameId.New(), "553850"), CancellationToken.None));

        Assert.Equal(ProviderFieldState.Value, patch.MultiPlayer.State);
        Assert.True(patch.MultiPlayer.Value);
        Assert.True(patch.OnlineCoop.Value);
        Assert.True(Apply(patch).SupportsCoop);
    }

    [Fact]
    public async Task Guild_wars_2_fixture_projects_structured_header_image()
    {
        var source = CreateSource(_ => Fixture("SteamStore/1284210.json"));
        var patch = Assert.Single(await source.GetAsync(Snapshot(GameId.New(), "1284210"), CancellationToken.None));
        Assert.Equal("1284210", patch.ProviderGameId);
        Assert.Equal(ProviderFieldState.Value, patch.ShortDescription.State);
    }

    [Fact]
    public async Task Local_coop_category_maps_to_local_coop_capability()
    {
        var source = CreateSource(_ => Json("{\"1\":{\"success\":true,\"data\":{\"steam_appid\":1,\"categories\":[{\"description\":\"Local Co-op\"}]}}}"));
        var patch = Assert.Single(await source.GetAsync(Snapshot(GameId.New(), "1"), CancellationToken.None));

        Assert.Equal(ProviderFieldState.Value, patch.LocalCoop.State);
        Assert.True(patch.LocalCoop.Value);
    }

    [Fact]
    public async Task Missing_store_fields_are_not_reported()
    {
        var source = CreateSource(_ => Json("{\"1203620\":{\"success\":true,\"data\":{\"steam_appid\":1203620,\"name\":\"Enshrouded\"}}}"));
        var patch = Assert.Single(await source.GetAsync(Snapshot(GameId.New(), "1203620"), CancellationToken.None));

        Assert.Equal(ProviderFieldState.NotReported, patch.ReleaseDate.State);
        Assert.Equal(ProviderFieldState.NotReported, patch.Genres.State);
        Assert.Equal(ProviderFieldState.NotReported, patch.OnlineCoop.State);
    }

    [Fact]
    public async Task Malformed_release_date_is_not_reported()
    {
        var source = CreateSource(_ => Json("{\"1203620\":{\"success\":true,\"data\":{\"steam_appid\":1203620,\"release_date\":{\"date\":\"unknown\"}}}}"));
        var patch = Assert.Single(await source.GetAsync(Snapshot(GameId.New(), "1203620"), CancellationToken.None));

        Assert.Equal(ProviderFieldState.NotReported, patch.ReleaseDate.State);
    }

    [Fact]
    public async Task Network_failure_returns_no_patch_and_preserves_existing_data()
    {
        var game = GameId.New();
        var existing = Metadata(game, "1203620", Now.AddDays(-8), complete: false) with { Developers = ["Known developer"] };
        var store = new MemoryStore(existing);
        var source = CreateSource(_ => throw new HttpRequestException("offline"), store);
        var sut = new ProviderGameMetadataReconciliationService(store, [source]);

        await sut.RefreshAsync(Snapshot(game, "1203620"), CancellationToken.None);

        Assert.Equal(["Known developer"], store.Value!.Developers);
        Assert.Null(store.Value.Genres);
    }

    [Fact]
    public async Task Fresh_complete_metadata_skips_network_refresh()
    {
        var game = GameId.New();
        var store = new MemoryStore(Metadata(game, "1203620", Now.AddDays(-1), complete: true));
        var requests = 0;
        var source = CreateSource(_ => { requests++; return Fixture("SteamStore/1203620.json"); }, store);

        await source.GetAsync(Snapshot(game, "1203620"), CancellationToken.None);

        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task Stale_or_incomplete_metadata_allows_enrichment()
    {
        var game = GameId.New();
        var store = new MemoryStore(Metadata(game, "1203620", Now.AddDays(-8), complete: true));
        var source = CreateSource(_ => Fixture("SteamStore/1203620.json"), store);

        Assert.NotEmpty(await source.GetAsync(Snapshot(game, "1203620"), CancellationToken.None));
    }

    [Fact]
    public async Task Duplicate_installations_coalesce_to_one_store_request()
    {
        var requests = 0;
        var source = CreateSource(_ => { Interlocked.Increment(ref requests); return Fixture("SteamStore/1203620.json"); });
        var snapshot = new LibrarySnapshot(
            [],
            [
                Installation(GameId.New(), "1203620"),
                Installation(GameId.New(), "1203620")
            ]);

        var patches = await source.GetAsync(snapshot, CancellationToken.None);

        Assert.Equal(1, requests);
        Assert.Equal(2, patches.Count);
    }

    [Fact]
    public async Task Store_requests_use_bounded_parallelism_of_four()
    {
        var active = 0;
        var maximum = 0;
        var source = new SteamStoreGameMetadataSource(
            new RecordingClient(async (_, cancellationToken) =>
            {
                var now = Interlocked.Increment(ref active);
                InterlockedExtensions.Max(ref maximum, now);
                await Task.Delay(25, cancellationToken);
                Interlocked.Decrement(ref active);
                return new SteamStoreAppDetails(1203620, ["Action"], ["Solo"], ["Developer"], ["Publisher"], new DateOnly(2024, 1, 1), false, "Description", null, null, null);
            }),
            new MemoryStore(),
            new FixedTimeProvider(Now));
        var installations = Enumerable.Range(1, 8)
            .Select(index => Installation(GameId.New(), index.ToString()))
            .ToArray();

        await source.GetAsync(new LibrarySnapshot([], installations), CancellationToken.None);

        Assert.InRange(maximum, 2, 4);
    }

    [Fact]
    public async Task Progress_reaches_total_when_store_requests_complete()
    {
        var source = new SteamStoreGameMetadataSource(
            new RecordingClient((_, _) => Task.FromResult<SteamStoreAppDetails?>(
                new SteamStoreAppDetails(1203620, ["Action"], ["Solo"], ["Developer"], ["Publisher"], new DateOnly(2024, 1, 1), false, "Description", null, null, null))),
            new MemoryStore(),
            new FixedTimeProvider(Now));
        var progress = new List<ProviderGameMetadataProgress>();
        source.ProgressChanged += (_, value) => progress.Add(value);

        await source.GetAsync(
            new LibrarySnapshot([], [Installation(GameId.New(), "1203620"), Installation(GameId.New(), "553850")]),
            CancellationToken.None);

        var final = Assert.Single(progress, value => !value.IsRunning);
        Assert.Equal(2, final.Total);
        Assert.Equal(2, final.Completed);
    }

    [Fact]
    public async Task Progress_total_uses_the_eligible_scan_set()
    {
        var eligible = new EligibleSnapshot(Enumerable.Range(1, 24).Select(x => x.ToString()));
        var source = new SteamStoreGameMetadataSource(
            new RecordingClient((_, _) => Task.FromResult<SteamStoreAppDetails?>(null)),
            new MemoryStore(),
            new FixedTimeProvider(Now),
            eligible);
        var installations = Enumerable.Range(1, 29)
            .Select(index => Installation(GameId.New(), index.ToString()))
            .ToArray();

        await source.GetAsync(new LibrarySnapshot([], installations), CancellationToken.None);

        Assert.Equal(24, source.Current.Total);
        Assert.Equal(24, source.Current.Completed);
    }

    [Fact]
    public async Task Manual_bridge_target_fans_out_a_steam_patch_with_exact_app_id()
    {
        var manualGame = GameId.New();
        var requests = new List<string>();
        var source = CreateSource(
            _ =>
            {
                requests.Add("2075800");
                return Json("{\"2075800\":{\"success\":true,\"data\":{\"steam_appid\":2075800,\"name\":\"Manual game\"}}}");
            },
            resolver: new TargetResolver(new ProviderGameMetadataTarget(manualGame, ProviderKind.Steam, "2075800", ProviderGameMetadataTargetOrigin.ManualBridge)));

        var patch = Assert.Single(await source.GetAsync(
            new LibrarySnapshot([], [Installation(manualGame, ProviderKind.Manual, "manual:game")]),
            CancellationToken.None));

        Assert.Equal(manualGame, patch.GameId);
        Assert.Equal(ProviderKind.Steam, patch.Provider);
        Assert.Equal("2075800", patch.ProviderGameId);
        Assert.Single(requests);
    }

    [Fact]
    public async Task Shared_app_id_is_requested_once_and_fanned_out_to_each_target()
    {
        var first = GameId.New();
        var second = GameId.New();
        var requests = 0;
        var source = CreateSource(
            _ =>
            {
                Interlocked.Increment(ref requests);
                return Fixture("SteamStore/1203620.json");
            },
            resolver: new TargetResolver(
                new ProviderGameMetadataTarget(first, ProviderKind.Steam, "1203620", ProviderGameMetadataTargetOrigin.ManualBridge),
                new ProviderGameMetadataTarget(second, ProviderKind.Steam, "1203620", ProviderGameMetadataTargetOrigin.ManualBridge)));

        var patches = await source.GetAsync(new LibrarySnapshot([], []), CancellationToken.None);

        Assert.Equal(1, requests);
        Assert.Equal(2, patches.Count);
        Assert.Equal(new[] { first, second }.OrderBy(x => x.Value), patches.Select(x => x.GameId).OrderBy(x => x.Value));
    }

    [Fact]
    public async Task Progress_is_counted_per_unique_source_identity()
    {
        var source = CreateSource(
            _ => Fixture("SteamStore/1203620.json"),
            resolver: new TargetResolver(
                new ProviderGameMetadataTarget(GameId.New(), ProviderKind.Steam, "1203620", ProviderGameMetadataTargetOrigin.Native),
                new ProviderGameMetadataTarget(GameId.New(), ProviderKind.Steam, "1203620", ProviderGameMetadataTargetOrigin.ManualBridge),
                new ProviderGameMetadataTarget(GameId.New(), ProviderKind.Steam, "553850", ProviderGameMetadataTargetOrigin.ManualBridge)));

        await source.GetAsync(new LibrarySnapshot([], []), CancellationToken.None);

        Assert.Equal(2, source.Current.Total);
        Assert.Equal(2, source.Current.Completed);
    }

    [Fact]
    public async Task Changed_provider_game_id_forces_refresh()
    {
        var game = GameId.New();
        var store = new MemoryStore(Metadata(game, "2075800", Now, complete: true));
        var requests = 0;
        var source = CreateSource(
            _ =>
            {
                Interlocked.Increment(ref requests);
                return Fixture("SteamStore/1203620.json");
            },
            store,
            resolver: new TargetResolver(new ProviderGameMetadataTarget(game, ProviderKind.Steam, "999999", ProviderGameMetadataTargetOrigin.ManualBridge)));

        _ = await source.GetAsync(new LibrarySnapshot([], []), CancellationToken.None);

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task Failure_for_one_source_identity_does_not_block_other_groups()
    {
        var source = CreateSource(
            uri => uri.Contains("553850", StringComparison.Ordinal)
                ? throw new HttpRequestException("offline")
                : Json("{\"1203620\":{\"success\":true,\"data\":{\"steam_appid\":1203620,\"name\":\"Game\"}}}"),
            resolver: new TargetResolver(
                new ProviderGameMetadataTarget(GameId.New(), ProviderKind.Steam, "1203620", ProviderGameMetadataTargetOrigin.Native),
                new ProviderGameMetadataTarget(GameId.New(), ProviderKind.Steam, "553850", ProviderGameMetadataTargetOrigin.ManualBridge)));

        var patches = await source.GetAsync(new LibrarySnapshot([], []), CancellationToken.None);

        Assert.Single(patches);
        Assert.Equal(1, source.Current.Failed);
        Assert.Equal(2, source.Current.Completed);
    }

    [Fact]
    public async Task Cancellation_is_propagated_without_being_counted_as_failure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source = new SteamStoreGameMetadataSource(
            new RecordingClient((_, token) => Task.FromCanceled<SteamStoreAppDetails?>(token)),
            new MemoryStore(),
            new FixedTimeProvider(Now),
            targetResolver: new TargetResolver(new ProviderGameMetadataTarget(GameId.New(), ProviderKind.Steam, "1203620", ProviderGameMetadataTargetOrigin.Native)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.GetAsync(new LibrarySnapshot([], []), cancellation.Token));
        Assert.Equal(0, source.Current.Failed);
    }

    [Fact]
    public async Task Stale_manual_bridge_target_is_not_reinserted_after_link_changes()
    {
        var target = new ProviderGameMetadataTarget(GameId.New(), ProviderKind.Steam, "3768760", ProviderGameMetadataTargetOrigin.ManualBridge);
        var source = CreateSource(
            _ => Json("{\"3768760\":{\"success\":true,\"data\":{\"steam_appid\":3768760,\"name\":\"Old link\"}}}"),
            resolver: new TargetResolver(target) { Current = false });

        Assert.Empty(await source.GetAsync(new LibrarySnapshot([], []), CancellationToken.None));
    }

    private SteamStoreGameMetadataSource CreateSource(
        Func<string, HttpContent> response,
        MemoryStore? store = null,
        IProviderGameMetadataTargetResolver? resolver = null)
    {
        store ??= new MemoryStore();
        var handler = new StubHandler(response);
        return new SteamStoreGameMetadataSource(new SteamStoreAppDetailsClient(new HttpClient(handler)), store, new FixedTimeProvider(Now), targetResolver: resolver);
    }

    private static HttpContent Fixture(string relative) => Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", relative.Replace('/', Path.DirectorySeparatorChar))));
    private static HttpContent Json(string value) => new StringContent(value, System.Text.Encoding.UTF8, "application/json");
    private static LibrarySnapshot Snapshot(GameId game, string appId) => new([], [Installation(game, appId)]);
    private static GameInstallation Installation(GameId game, string appId) => new(InstallationId.New(), game, ProviderKind.Steam, appId, "C:\\Games", null, true, true, Now);

    private static GameInstallation Installation(GameId game, ProviderKind provider, string externalId) =>
        new(InstallationId.New(), game, provider, externalId, "C:\\Games", null, true, true, Now);

    private static ProviderGameMetadata Metadata(GameId game, string appId, DateTimeOffset refreshed, bool complete) =>
        ProviderGameMetadata.Create(game, ProviderKind.Steam, appId, refreshed,
            genres: complete ? ["Action"] : null,
            categories: complete ? ["Single-player"] : null,
            developers: ["Keen Games GmbH"], publishers: ["Keen Games GmbH"],
            releaseDate: complete ? new DateOnly(2024, 1, 24) : null,
            isFree: false, singlePlayer: true, multiPlayer: true, onlineCoop: true, localCoop: false,
            availability: ProviderGameMetadataAvailability.Complete);

    private sealed class EligibleSnapshot(IEnumerable<string> ids) : ISteamEligibleInstallationSnapshot
    {
        public IReadOnlySet<string> EligibleExternalIds { get; } = ids.ToHashSet(StringComparer.Ordinal);
    }

    private static ProviderGameMetadata Apply(ProviderGameMetadataPatch patch) =>
        ProviderGameMetadata.Create(patch.GameId, patch.Provider, patch.ProviderGameId, Now,
            genres: patch.Genres.Value, categories: patch.Categories.Value,
            developers: patch.Developers.Value, publishers: patch.Publishers.Value,
            releaseDate: patch.ReleaseDate.Value, isFree: patch.IsFree.Value,
            singlePlayer: patch.SinglePlayer.Value, multiPlayer: patch.MultiPlayer.Value,
            onlineCoop: patch.OnlineCoop.Value, localCoop: patch.LocalCoop.Value,
            availability: patch.Availability);

    private sealed class StubHandler(Func<string, HttpContent> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = response(request.RequestUri!.ToString()) });
    }

    private sealed class MemoryStore(params ProviderGameMetadata[] values) : IProviderGameMetadataStore
    {
        private readonly List<ProviderGameMetadata> _values = values.ToList();
        public ProviderGameMetadata? Value => _values.FirstOrDefault();
        public Task<IReadOnlyList<ProviderGameMetadata>> GetAllAsync(CancellationToken _) => Task.FromResult<IReadOnlyList<ProviderGameMetadata>>(_values);
        public Task<ProviderGameMetadata?> GetAsync(GameId gameId, ProviderKind provider, CancellationToken _) => Task.FromResult(_values.FirstOrDefault(x => x.GameId == gameId && x.Provider == provider));
        public Task UpsertAsync(ProviderGameMetadata metadata, CancellationToken _) { _values.RemoveAll(x => x.GameId == metadata.GameId && x.Provider == metadata.Provider); _values.Add(metadata); return Task.CompletedTask; }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingClient(
        Func<string, CancellationToken, Task<SteamStoreAppDetails?>> callback) : ISteamStoreAppDetailsClient
    {
        public Task<SteamStoreAppDetails?> GetAsync(string appId, CancellationToken cancellationToken) =>
            callback(appId, cancellationToken);
    }

    private sealed class TargetResolver(params ProviderGameMetadataTarget[] targets) : IProviderGameMetadataTargetResolver
    {
        public bool Current { get; set; } = true;
        public Task<IReadOnlyList<ProviderGameMetadataTarget>> ResolveAsync(LibrarySnapshot snapshot, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderGameMetadataTarget>>(targets);
        public Task<bool> IsCurrentAsync(ProviderGameMetadataTarget target, CancellationToken cancellationToken) => Task.FromResult(Current);
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int location, int value)
        {
            while (true)
            {
                var current = Volatile.Read(ref location);
                if (value <= current || Interlocked.CompareExchange(ref location, value, current) == current)
                    return;
            }
        }
    }
}
