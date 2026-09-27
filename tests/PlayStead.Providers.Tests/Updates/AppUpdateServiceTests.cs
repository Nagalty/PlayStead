using PlayStead.Core.Updates;
using PlayStead.Providers.Updates;
using System.Net;
using System.Net.Http;
using System.Text;

namespace PlayStead.Providers.Tests.Updates;

public sealed class AppUpdateServiceTests
{
    [Fact]
    public async Task GitHub_service_without_manifest_source_does_not_use_network()
    {
        var service = new GitHubAppUpdateService("0.4.1-dev");

        var state = await service.CheckAsync();

        Assert.Equal(AppUpdateStatus.Unknown, state.Status);
        Assert.Equal(AppUpdateActionKind.None, state.Action);
    }

    [Fact]
    public async Task Microsoft_store_without_product_id_has_no_action()
    {
        var service = new MicrosoftStoreAppUpdateService(
            "0.4.1-dev",
            new MicrosoftStoreUpdateOptions());

        var state = await service.CheckAsync();

        Assert.Equal(AppUpdateStatus.Unknown, state.Status);
        Assert.Null(state.ActionUri);
    }

    [Fact]
    public async Task GitHub_manifest_maps_to_download_action_without_downloading()
    {
        var service = new GitHubAppUpdateService(
            "0.4.1-dev",
            _ => Task.FromResult<GitHubUpdateManifest?>(new GitHubUpdateManifest(
                "0.4.3",
                DistributionChannel.GitHub,
                new Uri("https://example.invalid/playstead.zip"),
                new string('a', 64))));

        var state = await service.CheckAsync();

        Assert.Equal(AppUpdateStatus.UpdateAvailable, state.Status);
        Assert.Equal(AppUpdateActionKind.DownloadAndInstall, state.Action);
        Assert.Equal("https://example.invalid/playstead.zip", state.ActionUri?.ToString());
    }

    [Fact]
    public async Task Microsoft_store_probe_reports_update_without_product_id_or_install_action()
    {
        var service = new MicrosoftStoreAppUpdateService(
            "0.4.1",
            new MicrosoftStoreUpdateOptions(),
            new FakeStoreProbe(true));

        var state = await service.CheckAsync();

        Assert.Equal(AppUpdateStatus.UpdateAvailable, state.Status);
        Assert.Equal(AppUpdateActionKind.OpenMicrosoftStore, state.Action);
        Assert.Null(state.ActionUri);
    }

    [Fact]
    public async Task Http_manifest_client_accepts_stable_https_manifest()
    {
        using var httpClient = new HttpClient(new JsonHandler("""
            {"channel":"stable","version":"0.4.3","publishedAtUtc":"2026-09-27T10:00:00Z","packageUrl":"https://github.com/example/playstead/releases/download/v0.4.3/PlayStead.zip","sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","releaseNotesUrl":"https://github.com/example/playstead/releases/tag/v0.4.3"}
            """));
        var client = new HttpGitHubUpdateManifestClient(httpClient);

        var manifest = await client.GetAsync(new Uri("https://example.invalid/manifest.json"), "stable");

        Assert.NotNull(manifest);
        Assert.Equal("0.4.3", manifest!.Version);
        Assert.Equal(DistributionChannel.GitHub, manifest.Channel);
    }

    [Fact]
    public async Task Stable_build_treats_stable_manifest_as_newer_than_alpha()
    {
        var manifest = new GitHubUpdateManifest(
            "0.4.3",
            DistributionChannel.GitHub,
            new Uri("https://example.invalid/package.zip"),
            new string('b', 64));

        var service = new GitHubAppUpdateService(
            "0.4.3-alpha1",
            _ => Task.FromResult<GitHubUpdateManifest?>(manifest),
            new GitHubUpdateOptions(ExpectedReleaseChannel: "stable"));

        var state = await service.CheckAsync();

        Assert.Equal(AppUpdateStatus.UpdateAvailable, state.Status);
    }

    [Fact]
    public async Task Alpha_build_uses_alpha_channel_by_default()
    {
        var manifest = new GitHubUpdateManifest(
            "0.4.3-alpha2",
            DistributionChannel.GitHub,
            new Uri("https://example.invalid/package.zip"),
            new string('c', 64))
        { ReleaseChannel = "alpha" };
        var service = new GitHubAppUpdateService(
            "0.4.3-alpha1",
            _ => Task.FromResult<GitHubUpdateManifest?>(manifest));

        var state = await service.CheckAsync();

        Assert.Equal(AppUpdateStatus.UpdateAvailable, state.Status);
        Assert.Equal("0.4.3-alpha2", state.AvailableVersion);
    }

    private sealed class FakeStoreProbe(bool hasUpdate) : IMicrosoftStoreUpdateProbe
    {
        public Task<bool> HasUpdateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(hasUpdate);
        }
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

}
