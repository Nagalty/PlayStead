using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.Data.Database;
using PlayStead.Data.Library;
using PlayStead.Data.Sessions;
using PlayStead.Data.Steam;
using PlayStead.Platform.Paths;
using PlayStead.Platform.Processes;
using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Evidence;
using PlayStead.Providers.Steam.Remote;
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

        builder.Services.AddSingleton<
            ILibraryStore,
            SqliteLibraryStore>();

        builder.Services.AddSingleton<
            ISteamEvidenceStore,
            SqliteSteamEvidenceStore>();

        builder.Services.AddSingleton<
            IProcessSignatureStore,
            SqliteProcessSignatureStore>();

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

        builder.Services.AddSingleton<
            ISessionRuntime,
            SessionRuntime>();

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
            LibraryViewModel>();

        builder.Services.AddSingleton<
            SessionViewModel>();

        builder.Services.AddSingleton<
            MainWindow>();

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
