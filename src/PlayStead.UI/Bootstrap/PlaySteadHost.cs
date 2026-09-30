using System.IO;
using System.Net.Http;
using System.Diagnostics;
using PlayStead.UI.Launching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlayStead.Core.Media;
using PlayStead.Core.Identity;
using PlayStead.Core.Notifications;
using PlayStead.Core.Home;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Core.Steam;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Core.GameBuildHistory;
using PlayStead.Data.ProviderActivity;
using PlayStead.Data.ProviderGameMetadata;
using PlayStead.Data.GameBuildHistory;
using PlayStead.Data.Database;
using PlayStead.Data.Catalog;
using PlayStead.Data.Library;
using PlayStead.Data.Identity;
using PlayStead.Data.Notifications;
using PlayStead.Data.Media;
using PlayStead.Data.Home;
using PlayStead.Data.Sessions;
using PlayStead.Data.Steam;
using PlayStead.Platform.Paths;
using PlayStead.Platform.Processes;
using PlayStead.Platform.Processes.Discovery;
using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Evidence;
using PlayStead.Providers.Steam.Media;
using PlayStead.Providers.Steam.Remote;
using PlayStead.UI.Home;
using PlayStead.UI.Attention;
using PlayStead.UI.Library;
using PlayStead.UI.Media;
using PlayStead.UI.Navigation;
using PlayStead.UI.Notifications;
using PlayStead.UI.Sessions;
using PlayStead.UI.Settings;
using PlayStead.UI.Shell;
using PlayStead.UI.SingleInstance;
using PlayStead.UI.State;
using PlayStead.UI.Steam;
using PlayStead.UI.Tray;
using PlayStead.UI.Updates;
using PlayStead.Core.Updates;
using PlayStead.Core.LocalArtifacts;
using PlayStead.Data.LocalArtifacts;
using PlayStead.Providers.Updates;

namespace PlayStead.UI.Bootstrap;

public static class PlaySteadHost
{
    public static IHost Build(
        UserDataLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var builder =
            Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(
            new DatabaseOptions(
                layout.DatabasePath,
                layout.BackupsDirectory));

        builder.Services.AddSingleton<
            DatabaseInitializer>();

        builder.Services.AddSingleton<
            DatabaseHealthChecker>();

        var dataRoot = Path.GetDirectoryName(layout.DatabasePath)
            ?? throw new InvalidOperationException("PlayStead database path has no parent directory.");
        builder.Services.AddSingleton(new AppUpdatePaths(Path.Combine(dataRoot, "Updates")));
        builder.Services.AddSingleton(new CatalogDatabaseOptions(
            Path.Combine(dataRoot, "catalog.db"),
            Path.Combine(layout.BackupsDirectory, "Catalog")));
        builder.Services.AddSingleton<CatalogDatabaseInitializer>();
        builder.Services.AddSingleton<StartupProgressState>();
        builder.Services.AddSingleton<SqliteCanonicalCatalogWriter>();
        builder.Services.AddSingleton<ICanonicalCatalogWriter>(
            services => services.GetRequiredService<SqliteCanonicalCatalogWriter>());
        builder.Services.AddSingleton<SteamAppInfoReader>();
        builder.Services.AddSingleton<SteamLocalCatalogImportSource>();
        builder.Services.AddSingleton<ISteamLocalCatalogImportSource>(services =>
            services.GetRequiredService<SteamLocalCatalogImportSource>());
        builder.Services.AddSingleton<SteamLocalCatalogBootstrapper>();
        builder.Services.AddSingleton<SqliteCanonicalCatalogStore>();
        builder.Services.AddSingleton<PlayStead.Core.Persistence.ICanonicalCatalogStore>(
            services => services.GetRequiredService<SqliteCanonicalCatalogStore>());

        builder.Services.AddSingleton<
            ILibraryStore,
            SqliteLibraryStore>();

        builder.Services.AddSingleton<
            IGameIdentityResolver,
            GameIdentityResolver>();

        builder.Services.AddSingleton<
            IIdentityResolutionStore,
            SqliteIdentityResolutionStore>();

        builder.Services.AddSingleton<
            IIdentityDecisionStore,
            SqliteIdentityDecisionStore>();

        builder.Services.AddSingleton<
            IIdentityDecisionService,
            SqliteIdentityDecisionService>();

        builder.Services.AddSingleton<
            IIdentityDecisionCandidateSource,
            EmptyIdentityDecisionCandidateSource>();

        builder.Services.AddSingleton<
            IIdentityDecisionContextProvider,
            IdentityDecisionContextProvider>();

        builder.Services.AddSingleton<
            IIdentityDecisionContextGateway,
            IdentityDecisionContextGateway>();

        builder.Services.AddSingleton<
            IIdentityDecisionNotificationOrchestrator,
            IdentityDecisionNotificationOrchestrator>();

        builder.Services.AddSingleton<
            IIdentityDecisionApplicationService,
            IdentityDecisionApplicationService>();

        builder.Services.AddSingleton<
            ILocalIdentityReconciler,
            SqliteLocalIdentityReconciler>();

        builder.Services.AddSingleton<
            ILibraryGameLookup,
            SqliteLibraryGameLookup>();

        builder.Services.AddSingleton<ILocalIdentityResolutionCoordinator>(services =>
            new LocalIdentityResolutionCoordinator(
                services.GetRequiredService<ILibraryGameLookup>(),
                services.GetRequiredService<IGameIdentityResolver>(),
                services.GetRequiredService<IIdentityResolutionStore>(),
                services.GetRequiredService<ILocalIdentityReconciler>(),
                services.GetRequiredService<IIdentityDecisionStore>(),
                services.GetRequiredService<IIdentityNotificationProducer>()));

        builder.Services.AddSingleton<INotificationStore, SqliteNotificationStore>();
        builder.Services.AddSingleton<INotificationCenterService, NotificationCenterService>();
        builder.Services.AddSingleton<IIdentityNotificationProducer, IdentityNotificationProducer>();
        builder.Services.AddSingleton<NotificationRetentionStartup>();

        builder.Services.AddSingleton<
            ISteamEvidenceStore,
            SqliteSteamEvidenceStore>();

        builder.Services.AddSingleton<SqliteProcessSignatureStore>();
        builder.Services.AddSingleton<IProcessSignatureStore>(services =>
            services.GetRequiredService<SqliteProcessSignatureStore>());
        builder.Services.AddSingleton<IProcessSignatureDiscoveryStore>(services =>
            services.GetRequiredService<SqliteProcessSignatureStore>());
        builder.Services.AddSingleton<IProcessSignatureLearningStore,
            SqliteProcessSignatureLearningStore>();
        builder.Services.AddSingleton<IExecutableInventorySource,
            WindowsExecutableInventorySource>();
        builder.Services.AddSingleton<IExecutableRevisionSource,
            WindowsExecutableRevisionSource>();
        builder.Services.AddSingleton<ProcessSignatureDiscoveryPolicy>();
        builder.Services.AddSingleton<ProcessSignatureLearningCoordinator>();
        builder.Services.AddSingleton<DiscoveryInventoryManager>();
        builder.Services.AddSingleton<DiscoveryConfirmationSessionPromoter>();
        builder.Services.AddSingleton<ProcessSignatureAcceptanceService>(services =>
            new ProcessSignatureAcceptanceService(
                services.GetRequiredService<IProcessSignatureLearningStore>(),
                services.GetRequiredService<IProcessSignatureStore>(),
                services.GetRequiredService<IProcessSignatureDiscoveryStore>(),
                services.GetRequiredService<IExecutableRevisionSource>(),
                services.GetRequiredService<ProcessSignatureDiscoveryPolicy>(),
                services.GetRequiredService<DiscoveryInventoryManager>().GetCurrent,
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<DiscoveryConfirmationSessionPromoter>()));
        builder.Services.AddSingleton<ProcessDiscoveryCaptureObserver>();
        builder.Services.AddSingleton<IProcessCaptureObserver>(services =>
            services.GetRequiredService<ProcessDiscoveryCaptureObserver>());
        builder.Services.AddSingleton<IDiscoveredSignatureValidator>(services =>
            new DiscoveredSignatureValidator(
                services.GetRequiredService<IProcessSignatureStore>(),
                services.GetRequiredService<IProcessSignatureLearningStore>(),
                services.GetRequiredService<IProcessSignatureDiscoveryStore>(),
                services.GetRequiredService<IExecutableRevisionSource>(),
                services.GetRequiredService<DiscoveryInventoryManager>().GetCurrent));

        builder.Services.AddSingleton<
            ISessionStore,
            SqliteSessionStore>();

        builder.Services.AddSingleton<
            ISessionCorrectionStore,
            SqliteSessionCorrectionStore>();

        builder.Services.AddSingleton<IProcessSnapshotSource>(services =>
            new WindowsProcessSnapshotSource(
                services.GetRequiredService<DiscoveryInventoryManager>()
                    .ContainsExecutableName));

        builder.Services.AddSingleton<
            ProcessSignatureMatcher>();

        builder.Services.AddSingleton<
            SessionTransitionPolicy>();

        builder.Services.AddSingleton<
            SessionCorrectionPolicy>();

        builder.Services.AddSingleton<ISessionRuntime>(services =>
            SessionRuntime.CreateWithObserver(
                services.GetRequiredService<IProcessSnapshotSource>(),
                services.GetRequiredService<IProcessSignatureStore>(),
                services.GetRequiredService<ISessionStore>(),
                services.GetRequiredService<ProcessSignatureMatcher>(),
                services.GetRequiredService<SessionTransitionPolicy>(),
                services.GetRequiredService<ISessionCorrectionStore>(),
                services.GetRequiredService<SessionCorrectionPolicy>(),
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<IProcessCaptureObserver>(),
                services.GetRequiredService<IDiscoveredSignatureValidator>()));

        builder.Services.AddSingleton(
            SessionMonitorOptions.Default);

        builder.Services.AddSingleton<
            SessionMonitor>();

        builder.Services.AddSingleton<IHostedService>(
            services =>
                services.GetRequiredService<
                    SessionMonitor>());

        builder.Services.AddSingleton<
            WindowsSteamRootLocator>(
            _ => new WindowsSteamRootLocator());

        builder.Services.AddSingleton<HttpClient>();

        builder.Services.AddSingleton<
            IGameMediaCache>(
            _ => new FileGameMediaCache(
                layout.MediaDirectory));

        builder.Services.AddSingleton<IMediaDiagnostics, TraceMediaDiagnostics>();

        builder.Services.AddSingleton<
            SteamLocalMediaLocator>();

        builder.Services.AddSingleton<
            ILocalGameMediaResolver,
            SteamLocalGameMediaResolver>();

        builder.Services.AddSingleton<
            ISteamMediaTransport,
            HttpSteamMediaTransport>();

        builder.Services.AddSingleton<IGameMediaProvider>(services =>
            new SteamMediaProvider(
                services.GetRequiredService<WindowsSteamRootLocator>(),
                services.GetRequiredService<SteamLocalMediaLocator>(),
                services.GetRequiredService<ISteamMediaTransport>(),
                storeClient: services.GetRequiredService<ISteamStoreAppDetailsClient>()));

        builder.Services.AddSingleton<
            IGameMediaResolver,
            GameMediaResolver>();

        builder.Services.AddSingleton<
            SteamLibraryFoldersReader>();

        builder.Services.AddSingleton<
            SteamAppManifestReader>();
        builder.Services.AddSingleton<SteamLocalConfigActivityReader>();

        builder.Services.AddSingleton<
            ILocalLibrarySource,
            SteamLocalLibrarySource>();
        builder.Services.AddSingleton<
            IProviderActivityMetadataStore,
            SqliteProviderActivityMetadataStore>();
        builder.Services.AddSingleton<
            IProviderObservedSessionStore,
            SqliteProviderObservedSessionStore>();
        builder.Services.AddSingleton<IEffectiveActivityService, EffectiveActivityService>();
        builder.Services.AddSingleton<SteamProcessLogSessionParser>();
        builder.Services.AddSingleton<SteamProcessLogSessionImporter>();
        builder.Services.AddSingleton<SteamLocalProviderActivitySource>();
        builder.Services.AddSingleton<IProviderActivityMetadataSource>(services =>
            services.GetRequiredService<SteamLocalProviderActivitySource>());
        builder.Services.AddSingleton<ProviderActivityReconciliationService>();
        builder.Services.AddSingleton<IProviderGameMetadataStore, SqliteProviderGameMetadataStore>();
        builder.Services.AddSingleton<PlayStead.Core.Modding.IModEvidenceStore, PlayStead.Data.Modding.SqliteModEvidenceStore>();
        builder.Services.AddSingleton<PlayStead.Core.Modding.IModEvidenceDetector, PlayStead.Providers.Steam.SteamWorkshopModEvidenceDetector>();
        builder.Services.AddSingleton<PlayStead.Core.Modding.IModEvidenceDetector, PlayStead.Providers.Steam.SteamGameSpecificModEvidenceDetector>();
        builder.Services.AddSingleton<PlayStead.Core.Modding.ModEvidenceRefreshService>();
        builder.Services.AddSingleton<SteamLocalGameMetadataSource>();
        builder.Services.AddSingleton<HttpClient>(_ =>
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri("https://store.steampowered.com/"),
                Timeout = TimeSpan.FromSeconds(7)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PlayStead/0.4.3");
            return client;
        });
        builder.Services.AddSingleton<ISteamStoreAppDetailsClient, SteamStoreAppDetailsClient>();
        builder.Services.AddSingleton<SteamStoreGameMetadataSource>();
        builder.Services.AddSingleton<ProviderGameMetadataReconciliationService>(services =>
            new ProviderGameMetadataReconciliationService(
                services.GetRequiredService<IProviderGameMetadataStore>(),
                [services.GetRequiredService<SteamLocalGameMetadataSource>()]));
        builder.Services.AddSingleton<ProviderGameMetadataOnlineReconciliationService>(services =>
            new ProviderGameMetadataOnlineReconciliationService(
                services.GetRequiredService<IProviderGameMetadataStore>(),
                [services.GetRequiredService<SteamStoreGameMetadataSource>()]));
        builder.Services.AddSingleton<IProviderGameMetadataProgress>(services =>
            services.GetRequiredService<ProviderGameMetadataOnlineReconciliationService>());
        builder.Services.AddSingleton<PlayStead.Core.ProviderInstallUpdate.ProviderInstallUpdateStateEvaluator>();
        builder.Services.AddSingleton<SteamLocalInstallUpdateStateSource>();
        builder.Services.AddSingleton<PlayStead.Core.ProviderInstallUpdate.IProviderInstallUpdateStateSource>(services =>
            services.GetRequiredService<SteamLocalInstallUpdateStateSource>());
        builder.Services.AddSingleton<IGameBuildHistoryStore, SqliteGameBuildHistoryStore>();
        builder.Services.AddSingleton<GameBuildHistoryService>(services =>
            new GameBuildHistoryService(
                services.GetRequiredService<IGameBuildHistoryStore>(),
                services.GetService<ISessionStore>()));
        builder.Services.AddSingleton<PlayStead.Core.ProviderInstallUpdate.ProviderInstallUpdateStateReconciliationService>(services =>
            new PlayStead.Core.ProviderInstallUpdate.ProviderInstallUpdateStateReconciliationService(
                services.GetServices<PlayStead.Core.ProviderInstallUpdate.IProviderInstallUpdateStateSource>(),
                services.GetRequiredService<GameBuildHistoryService>(),
                services.GetRequiredService<PlayStead.Core.ProviderInstallUpdate.IProviderInstallUpdateProtectionService>()));
        builder.Services.AddSingleton<SteamInstallUpdateLiveRefreshService>();
        builder.Services.AddSingleton<IHomeSuggestionSelectionStore>(_ =>
            new JsonHomeSuggestionSelectionStore(
                Path.Combine(dataRoot, "home-suggestion-selection.json")));
        builder.Services.AddSingleton<HomeSuggestionSelector>();

        builder.Services.AddSingleton<
            SteamLocalEvidenceReader>();

        builder.Services.AddSingleton<
            ISteamLocalEvidenceSource,
            SteamLocalEvidenceSource>();

        builder.Services.AddSingleton<
            SteamUpdateStateEvaluator>();

        builder.Services.AddSingleton<
            TimeProvider>(
            TimeProvider.System);

        builder.Services.AddSingleton(
            SteamCmdOptions.Default);

        builder.Services.AddSingleton<
            SteamCmdPathResolver>();

        builder.Services.AddSingleton<
            ISteamCmdProcessInvoker,
            SystemSteamCmdProcessInvoker>();

        builder.Services.AddSingleton<
            ISteamCmdRunner,
            SteamCmdRunner>();

        builder.Services.AddSingleton<
            ISteamCmdAppInfoParser,
            SteamCmdAppInfoParser>();

        builder.Services.AddSingleton<
            ISteamRemoteEvidenceProvider,
            SteamRemoteEvidenceProvider>();

        builder.Services.AddSingleton<
            SteamRemoteEvidenceFreshnessPolicy>();

        builder.Services.AddSingleton<
            SteamRemoteRefreshCoordinator>();

        builder.Services.AddSingleton<
            ISteamReferenceRuntime,
            SteamReferenceRuntime>();

        builder.Services.AddSingleton<
            LocalScanCoordinator>();

        builder.Services.AddSingleton<
            LocalStartupPipeline>();

        builder.Services.AddSingleton<
            ApplicationRuntime>();

        builder.Services.AddSingleton(
            new WindowPlacementService(
                Path.Combine(
                    Path.GetDirectoryName(
                        layout.DatabasePath)!,
                    "window-placement.json")));

        builder.Services.AddSingleton(
            new UiPreferencesStore(
                Path.Combine(
                    Path.GetDirectoryName(
                        layout.DatabasePath)!,
                    "ui-preferences.json")));

        builder.Services.AddSingleton<
            SettingsViewModel>();

        builder.Services.AddSingleton<
            UiMotionController>();

        builder.Services.AddSingleton<
            WindowClosePolicy>();

        builder.Services.AddSingleton<
            NavigationService>();

        builder.Services.AddSingleton<
            ShellViewModel>();

        builder.Services.AddSingleton<IUserDefinedLocalArtifactStore, SqliteUserDefinedLocalArtifactStore>();
        builder.Services.AddSingleton<UserDefinedLocalArtifactService>(services =>
            new UserDefinedLocalArtifactService(
                services.GetRequiredService<IUserDefinedLocalArtifactStore>(),
                LocalArtifactRuleCatalog.Rules));
        builder.Services.AddSingleton<IGameLocalArtifactDiscoveryService>(
            services => new LocalArtifactDiscoveryService(LocalArtifactRuleCatalog.Rules, services.GetRequiredService<IUserDefinedLocalArtifactStore>()));
        builder.Services.AddSingleton<IArtifactFingerprintService, Sha256ArtifactFingerprintService>();
        builder.Services.AddSingleton<ILocalArtifactBaselineStore, SqliteLocalArtifactBaselineStore>();
        builder.Services.AddSingleton<LocalArtifactBaselineComparisonService>();
        builder.Services.AddSingleton<ILocalArtifactComparisonService, LocalArtifactComparisonService>();
        builder.Services.AddSingleton<ILocalProtectionSetupService, LocalProtectionSetupService>();
        builder.Services.AddSingleton<ILocalArtifactSnapshotStore, SqliteLocalArtifactSnapshotStore>();
        builder.Services.AddSingleton<ILocalSnapshotStorageService>(services =>
            new LocalSnapshotStorageService(
                services.GetRequiredService<ILocalArtifactSnapshotStore>()));
        builder.Services.AddSingleton<ILocalArtifactSnapshotService>(services =>
            new LocalArtifactSnapshotService(
                services.GetRequiredService<IArtifactFingerprintService>(),
                services.GetRequiredService<ILocalArtifactSnapshotStore>(),
                Path.Combine(dataRoot, "Snapshots"),
                services.GetRequiredService<ILocalSnapshotStorageService>()));
        builder.Services.AddSingleton<PlayStead.Core.ProviderInstallUpdate.IWindowsSilentNotificationSink>(services =>
            services.GetRequiredService<TrayIconService>());
        builder.Services.AddSingleton<PlayStead.Core.ProviderInstallUpdate.IProviderInstallUpdateProtectionService>(services =>
            new PlayStead.Core.ProviderInstallUpdate.ProviderInstallUpdateProtectionService(
                services.GetRequiredService<PlayStead.Core.LocalArtifacts.ILocalProtectionSetupService>(),
                services.GetRequiredService<PlayStead.Core.LocalArtifacts.ILocalArtifactSnapshotService>(),
                services.GetRequiredService<PlayStead.Core.Notifications.INotificationCenterService>(),
                services.GetRequiredService<PlayStead.Core.Persistence.ILibraryStore>(),
                services.GetRequiredService<PlayStead.Core.ProviderInstallUpdate.IWindowsSilentNotificationSink>()));
        builder.Services.AddSingleton<ILocalArtifactRestoreService>(services =>
            new LocalArtifactRestoreService(
                services.GetRequiredService<IArtifactFingerprintService>(),
                services.GetRequiredService<ILocalArtifactSnapshotService>(),
                gameId => services.GetRequiredService<SessionMonitor>().LatestSnapshot?.ActiveSessions.Any(session => session.GameId == gameId.Value) == true));

        builder.Services.AddSingleton<
            PlayStead.Core.Collections.IGameCollectionStore,
            PlayStead.Data.Collections.SqliteGameCollectionStore>();

        builder.Services.AddSingleton<
            LibraryViewModel>(
            services =>
            {
                var viewModel = new LibraryViewModel(
                    services.GetRequiredService<ILibraryStore>(),
                    services.GetRequiredService<ISteamReferenceRuntime>(),
                    services.GetRequiredService<SessionMonitor>(),
                    services.GetRequiredService<UiPreferencesStore>(),
                    services.GetRequiredService<IGameMediaResolver>(),
                    services.GetRequiredService<ICanonicalCatalogStore>(),
                    services.GetRequiredService<PlayStead.Core.Shortlist.IGamesDuMomentService>(),
                    services.GetRequiredService<IProviderGameMetadataStore>(),
                    services.GetRequiredService<IProviderActivityMetadataStore>(),
                    services.GetRequiredService<ISessionStore>());
                viewModel.AttachCollectionStore(
                    services.GetRequiredService<PlayStead.Core.Collections.IGameCollectionStore>());
                viewModel.AttachAttentionService(
                    services.GetRequiredService<PlayStead.Core.Notifications.IAttentionService>());
                viewModel.AttachModEvidenceStore(
                    services.GetRequiredService<PlayStead.Core.Modding.IModEvidenceStore>());
                viewModel.AttachModEvidenceRefreshService(
                    services.GetRequiredService<PlayStead.Core.Modding.ModEvidenceRefreshService>());
                viewModel.AttachLocalProtectionSetupService(
                    services.GetRequiredService<PlayStead.Core.LocalArtifacts.ILocalProtectionSetupService>());
                return viewModel;
            });

        builder.Services.AddSingleton<
            SessionViewModel>();

        builder.Services.AddSingleton<
            PlayStead.Core.Shortlist.IGamesDuMomentService,
            PlayStead.Data.Shortlist.SqliteGamesDuMomentService>();
        builder.Services.AddSingleton<PlayStead.Core.Sessions.IWeeklyActivitySummaryService>(services =>
            new PlayStead.Data.Sessions.SqliteWeeklyActivitySummaryService(
                services.GetRequiredService<ISessionStore>(),
                services.GetRequiredService<IProviderObservedSessionStore>()));

        builder.Services.AddSingleton<
            HomeViewModel>();

        builder.Services.AddSingleton<PlayStead.Core.Notifications.IAttentionService>(services =>
            new PlayStead.Core.Notifications.NotificationAttentionService(
                services.GetRequiredService<PlayStead.Core.Notifications.INotificationCenterService>(),
                services.GetRequiredService<PlayStead.Core.ProviderInstallUpdate.ProviderInstallUpdateStateReconciliationService>(),
                services.GetRequiredService<PlayStead.Core.Persistence.ILibraryStore>()));
        builder.Services.AddSingleton<AttentionViewModel>(services =>
            new AttentionViewModel(services.GetRequiredService<PlayStead.Core.Notifications.IAttentionService>()));

        builder.Services.AddSingleton<
            NotificationCenterViewModel>();

        builder.Services.AddSingleton<
            MainWindow>();

        builder.Services.AddSingleton<IExternalUriLauncher>(
            _ => new ShellExternalUriLauncher(startInfo =>
            {
                using var process = Process.Start(startInfo);
            }));
        builder.Services.AddSingleton<GameLaunchService>();

        builder.Services.AddSingleton<
            IWindowActivator,
            MainWindowActivator>();

        builder.Services.AddSingleton<
            IAppInvocationHandler,
            AppInvocationHandler>();

        builder.Services.AddSingleton<
            TrayIconService>();

        builder.Services.AddSingleton<IDistributionChannelProvider>(_ =>
            new BuildDistributionChannelProvider(typeof(PlaySteadHost).Assembly));
        builder.Services.AddSingleton(new GitHubUpdateOptions(
            ManifestUrl: Environment.GetEnvironmentVariable("PLAYSTEAD_GITHUB_UPDATE_MANIFEST_URL")));
        builder.Services.AddSingleton(new MicrosoftStoreUpdateOptions());
        builder.Services.AddSingleton<IGitHubUpdateManifestClient>(services =>
        {
            var options = services.GetRequiredService<GitHubUpdateOptions>();
            var client = new HttpClient
            {
                Timeout = options.Timeout ?? TimeSpan.FromSeconds(5)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PlayStead/0.4.3");
            return new HttpGitHubUpdateManifestClient(client);
        });
        builder.Services.AddSingleton<IAppUpdatePackageDownloader>(services =>
        {
            var client = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PlayStead/0.4.3");
            return new GitHubAppUpdatePackageDownloader(
                client,
                services.GetRequiredService<AppUpdatePaths>().UpdatesRoot);
        });
        builder.Services.AddSingleton<IMicrosoftStoreUpdateProbe, ReflectionMicrosoftStoreUpdateProbe>();
        builder.Services.AddSingleton<IAppUpdateService>(services =>
        {
            var channel = services.GetRequiredService<IDistributionChannelProvider>().Current;
            return channel switch
            {
                DistributionChannel.MicrosoftStore =>
                    new MicrosoftStoreAppUpdateService(
                        PlayStead.Core.Product.ProductVersion.Current,
                        services.GetRequiredService<MicrosoftStoreUpdateOptions>(),
                        services.GetRequiredService<IMicrosoftStoreUpdateProbe>()),
                _ => new GitHubAppUpdateService(
                    PlayStead.Core.Product.ProductVersion.Current,
                    options: services.GetRequiredService<GitHubUpdateOptions>(),
                    client: services.GetRequiredService<IGitHubUpdateManifestClient>())
            };
        });
          builder.Services.AddSingleton<AppUpdateCoordinator>();
          builder.Services.AddSingleton<IAppUpdateInstaller>(services =>
              new ExternalAppUpdateInstaller(services.GetRequiredService<AppUpdatePaths>()));
        builder.Services.AddSingleton<AppUpdateNotificationViewModel>();

        return builder.Build();
    }
}
