using Microsoft.Extensions.DependencyInjection;
using PlayStead.Core.Media;
using PlayStead.Data.Media;
using PlayStead.Platform.Paths;
using PlayStead.Providers.Steam.Media;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class MediaServiceRegistrationTests :
    IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public void Build_registers_media_pipeline_and_resolves_production_LibraryViewModel()
    {
        var layout =
            UserDataLayout.FromRoot(
                _root);

        layout.EnsureDirectoriesExist();

        using var host =
            PlaySteadHost.Build(
                layout);

        var cache =
            host.Services.GetRequiredService<
                IGameMediaCache>();

        var httpClient =
            host.Services.GetRequiredService<
                HttpClient>();

        var transport =
            host.Services.GetRequiredService<
                ISteamMediaTransport>();

        var localLocator =
            host.Services.GetRequiredService<
                SteamLocalMediaLocator>();

        var localResolver =
            host.Services.GetRequiredService<
                ILocalGameMediaResolver>();

        var provider =
            host.Services.GetRequiredService<
                IGameMediaProvider>();

        var resolver =
            host.Services.GetRequiredService<
                IGameMediaResolver>();

        var libraryViewModel =
            host.Services.GetRequiredService<
                LibraryViewModel>();

        Assert.IsType<FileGameMediaCache>(
            cache);

        Assert.NotNull(
            httpClient);

        Assert.IsType<HttpSteamMediaTransport>(
            transport);

        Assert.NotNull(
            localLocator);

        Assert.IsType<SteamLocalGameMediaResolver>(
            localResolver);

        Assert.IsType<SteamMediaProvider>(
            provider);

        Assert.IsType<GameMediaResolver>(
            resolver);

        Assert.NotNull(
            libraryViewModel);
    }

    [Fact]
    public void Build_uses_singleton_media_services()
    {
        var layout =
            UserDataLayout.FromRoot(
                _root);

        layout.EnsureDirectoriesExist();

        using var host =
            PlaySteadHost.Build(
                layout);

        Assert.Same(
            host.Services.GetRequiredService<
                IGameMediaCache>(),
            host.Services.GetRequiredService<
                IGameMediaCache>());

        Assert.Same(
            host.Services.GetRequiredService<
                HttpClient>(),
            host.Services.GetRequiredService<
                HttpClient>());

        Assert.Same(
            host.Services.GetRequiredService<
                ISteamMediaTransport>(),
            host.Services.GetRequiredService<
                ISteamMediaTransport>());

        Assert.Same(
            host.Services.GetRequiredService<
                IGameMediaResolver>(),
            host.Services.GetRequiredService<
                IGameMediaResolver>());
    }

    public void Dispose()
    {
        if (Directory.Exists(
                _root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }
}
