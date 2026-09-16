using System.IO;
using System.Net.Http;
using System.Diagnostics;
using PlayStead.UI.Launching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlayStead.Core.Media;
using PlayStead.Core.Identity;
using PlayStead.Core.Notifications;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Core.Steam;
using PlayStead.Data.Database;
using PlayStead.Data.Catalog;
using PlayStead.Data.Library;
using PlayStead.Data.Identity;
using PlayStead.Data.Notifications;
using PlayStead.Data.Media;
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
using PlayStead.UI.Navigation;
using PlayStead.UI.Sessions;
using PlayStead.UI.Settings;
using PlayStead.UI.Shell;
using PlayStead.UI.SingleInstance;
using PlayStead.UI.State;
using PlayStead.UI.Steam;
using PlayStead.UI.Tray;

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
        builder.Services.AddSingleton(new CatalogDatabaseOptions(
            Path.Combine(dataRoot, "catalog.db"),
            Path.Combine(layout.BackupsDirectory, "Catalog")));
        builder.Services.AddSingleton<CatalogDatabaseInitializer>();
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
            ILocalIdentityReconciler,
            SqliteLocalIdentityReconciler>();

        builder.Services.AddSingleton<
            ILibraryGameLookup,
            SqliteLibraryGameLookup>();

        builder.Services.AddSingleton<
            ILocalIdentityResolutionCoordinator,
            LocalIdentityResolutionCoordinator>();

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
        builder.Services.AddSingleton<ProcessSignatureAcceptanceService>(services =>
            new ProcessSignatureAcceptanceService(
                services.GetRequiredService<IProcessSignatureLearningStore>(),
                services.GetRequiredService<IProcessSignatureStore>(),
                services.GetRequiredService<IProcessSignatureDiscoveryStore>(),
                services.GetRequiredService<IExecutableRevisionSource>(),
                services.GetRequiredService<ProcessSignatureDiscoveryPolicy>(),
                services.GetRequiredService<DiscoveryInventoryManager>().GetCurrent,
                services.GetRequiredService<TimeProvider>()));
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

        builder.Services.AddSingleton<
            IProcessSnapshotSource,
            WindowsProcessSnapshotSource>();

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

        builder.Services.AddSingleton<
            SteamLocalMediaLocator>();

        builder.Services.AddSingleton<
            ISteamMediaTransport,
            HttpSteamMediaTransport>();

        builder.Services.AddSingleton<
            IGameMediaProvider,
            SteamMediaProvider>();

        builder.Services.AddSingleton<
            IGameMediaResolver,
            GameMediaResolver>();

        builder.Services.AddSingleton<
            SteamLibraryFoldersReader>();

        builder.Services.AddSingleton<
            SteamAppManifestReader>();

        builder.Services.AddSingleton<
            ILocalLibrarySource,
            SteamLocalLibrarySource>();

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

        builder.Services.AddSingleton<
            LibraryViewModel>(
            services =>
                new LibraryViewModel(
                    services.GetRequiredService<
                        ILibraryStore>(),
                    services.GetRequiredService<
                        ISteamReferenceRuntime>(),
                    services.GetRequiredService<
                        SessionMonitor>(),
                    services.GetRequiredService<
                        UiPreferencesStore>(),
                    services.GetRequiredService<
                        IGameMediaResolver>()));

        builder.Services.AddSingleton<
            SessionViewModel>();

        builder.Services.AddSingleton<
            HomeViewModel>();

        builder.Services.AddSingleton<
            AttentionViewModel>();

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

        return builder.Build();
    }
}
