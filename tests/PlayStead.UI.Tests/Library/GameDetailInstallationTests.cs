using PlayStead.Core.Library;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;
using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailInstallationTests
{
    [Fact]
    public async Task Update_state_projection_explains_blocked_launch()
    {
        var item = new LibraryItemViewModel(GameId.New(), "Hell Let Loose", ProviderKind.Epic, "hll", @"C:\Games\Hll", null, null);
        var installation = new GameInstallation(InstallationId.New(), item.GameId, ProviderKind.Epic, "hll", item.InstallPath, null, true, true, DateTimeOffset.UtcNow);
        var updates = new ProviderInstallUpdateStateReconciliationService([new FixedUpdateSource(installation, ProviderInstallUpdateStatus.Downloading)]);
        await updates.RefreshAsync([installation], CancellationToken.None);

        var viewModel = new GameDetailViewModel(item, null, null, null, installUpdates: updates);

        Assert.True(viewModel.HasInstallUpdateState);
        Assert.True(viewModel.IsLaunchBlockedByInstallState);
        Assert.Equal("Mise à jour en cours", viewModel.InstallUpdateStatusText);
        Assert.Contains("terminée", viewModel.InstallUpdateDetailText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Current_update_state_hides_installation_warning()
    {
        var item = new LibraryItemViewModel(GameId.New(), "Game", ProviderKind.Steam, "steam", @"C:\Games\Game", null, null);
        var installation = new GameInstallation(InstallationId.New(), item.GameId, ProviderKind.Steam, "steam", item.InstallPath, null, true, true, DateTimeOffset.UtcNow);
        var updates = new ProviderInstallUpdateStateReconciliationService([new FixedUpdateSource(installation, ProviderInstallUpdateStatus.UpToDate)]);
        await updates.RefreshAsync([installation], CancellationToken.None);

        var viewModel = new GameDetailViewModel(item, null, null, null, installUpdates: updates);

        Assert.False(viewModel.HasInstallUpdateState);
        Assert.False(viewModel.IsLaunchBlockedByInstallState);
        Assert.Equal(string.Empty, viewModel.InstallUpdateStatusText);
    }

    [Fact]
    public void Installation_module_exposes_real_local_fields()
    {
        var item = new LibraryItemViewModel(GameId.New(), "Game", ProviderKind.Steam, "Steam", @"C:\Games\Game", 42_000_000_000, SteamUpdateState.UpToDate, IsSessionActive: true);
        var viewModel = new GameDetailViewModel(item);
        Assert.Equal("Steam", viewModel.ProviderLabel);
        Assert.Equal(@"C:\Games\Game", viewModel.InstallPath);
        Assert.Equal("C:", viewModel.InstallDriveLabel);
        Assert.Equal("42,0 Go", viewModel.InstalledSizeLabel);
        Assert.Equal("À jour", viewModel.SteamStatusLabel);
        Assert.Equal("En cours", viewModel.SessionStatusLabel);
    }

    [Fact]
    public void Manual_game_detail_uses_persisted_install_root_and_keeps_retail_working_directory_out_of_projection()
    {
        var item = new LibraryItemViewModel(
            GameId.New(),
            "007 First Light",
            ProviderKind.Manual,
            "Manuel",
            @"H:\007 First Light",
            null,
            null,
            IsSessionActive: false);

        var viewModel = new GameDetailViewModel(item);

        Assert.Equal(@"H:\007 First Light", viewModel.InstallPath);
        Assert.Equal("H:", viewModel.InstallDriveLabel);
    }

    [Fact]
    public void Installation_projection_exposes_explicit_fallbacks_when_fields_are_missing()
    {
        var item = new LibraryItemViewModel(
            GameId.New(),
            "Unknown install",
            ProviderKind.Manual,
            "Manuel",
            string.Empty,
            null,
            null,
            IsSessionActive: false);

        var viewModel = new GameDetailViewModel(item);

        Assert.Equal("Taille inconnue", viewModel.InstalledSizeLabel);
        Assert.Equal("—", viewModel.InstallDriveDisplay);
        Assert.Equal("—", viewModel.InstallPathDisplay);
    }
    [Fact]
    public void Installation_view_exposes_authoritative_local_fields()
    {
        var xaml = File.ReadAllText(FindUiFile(Path.Combine("Library", "GameDetailView.xaml")));
        Assert.Contains("x:Name=\"InstallationPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Title=\"Installation\"", xaml, StringComparison.Ordinal);
        Assert.Contains("InstalledSizeLabel", xaml, StringComparison.Ordinal);
        Assert.Contains("InstallDriveDisplay", xaml, StringComparison.Ordinal);
        Assert.Contains("InstallPathDisplay", xaml, StringComparison.Ordinal);
        Assert.Contains("BooleanToVisibilityConverter", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_does_not_display_internal_ids_or_future_metadata()
    {
        var xaml =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml")));

        Assert.DoesNotContain(
            "CanonicalContentId",
            xaml,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "SteamGridDB",
            xaml,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "IGDB",
            xaml,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string FindUiFile(string relativePath)
    {
        var directory =
            new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var uiDirectory =
                Path.Combine(directory.FullName, "src", "PlayStead.UI");

            if (Directory.Exists(uiDirectory))
            {
                var path = Path.Combine(uiDirectory, relativePath);
                Assert.True(File.Exists(path), $"Required UI file missing: {relativePath}");
                return path;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "PlayStead.UI source directory was not found.");
    }

    private sealed class FixedUpdateSource(GameInstallation installation, ProviderInstallUpdateStatus status) : IProviderInstallUpdateStateSource
    {
        public ProviderKind Provider => installation.Provider;

        public Task<IReadOnlyList<ProviderInstallUpdateState>> GetAsync(IReadOnlyCollection<GameInstallation> installations, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderInstallUpdateState>>([new ProviderInstallUpdateState(
                installation.GameId, installation.Provider, installation.ExternalId, null, null, status,
                null, null, null, null, null, null, DateTimeOffset.UtcNow)]);
    }
}
