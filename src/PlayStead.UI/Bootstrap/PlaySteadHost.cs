using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.Data.Library;
using PlayStead.Platform.Paths;
using PlayStead.Providers.Steam;
using PlayStead.UI.Library;
using PlayStead.UI.SingleInstance;
using PlayStead.UI.State;

namespace PlayStead.UI.Bootstrap;

public static class PlaySteadHost
{
    public static IHost Build(UserDataLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(
            new DatabaseOptions(
                layout.DatabasePath,
                layout.BackupsDirectory));

        builder.Services.AddSingleton<DatabaseInitializer>();
        builder.Services.AddSingleton<DatabaseHealthChecker>();

        builder.Services.AddSingleton<ILibraryStore, SqliteLibraryStore>();

        builder.Services.AddSingleton<WindowsSteamRootLocator>(
            _ => new WindowsSteamRootLocator());
        builder.Services.AddSingleton<SteamLibraryFoldersReader>();
        builder.Services.AddSingleton<SteamAppManifestReader>();
        builder.Services.AddSingleton<ILocalLibrarySource, SteamLocalLibrarySource>();

        builder.Services.AddSingleton<LocalScanCoordinator>();
        builder.Services.AddSingleton<LocalStartupPipeline>();
        builder.Services.AddSingleton<ApplicationRuntime>();

        builder.Services.AddSingleton(
            new WindowPlacementService(
                Path.Combine(
                    Path.GetDirectoryName(layout.DatabasePath)!,
                    "window-placement.json")));

        builder.Services.AddSingleton<LibraryViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        builder.Services.AddSingleton<IWindowActivator, MainWindowActivator>();
        builder.Services.AddSingleton<IAppInvocationHandler, AppInvocationHandler>();

        return builder.Build();
    }
}
