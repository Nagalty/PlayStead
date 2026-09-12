using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlayStead.Platform.Paths;
using PlayStead.Platform.SingleInstance;
using PlayStead.UI.Bootstrap;
using PlayStead.UI.Library;
using System.Windows;
using System.Windows.Threading;

namespace PlayStead.UI;

public partial class App : Application
{
    private const string MutexName =
        @"Local\PlayStead";

    private const string PipeName =
        "PlayStead.Invocation";

    private readonly CancellationTokenSource _lifetime =
        new();

    private ApplicationStartupCoordinator? _startupCoordinator;
    private SingleInstanceGate? _singleInstanceGate;
    private NamedPipeInvocationServer? _invocationServer;
    private IHost? _host;

    protected override async void OnStartup(
        StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _singleInstanceGate =
                new SingleInstanceGate(
                    MutexName,
                    PipeName);

            _invocationServer =
                new NamedPipeInvocationServer(
                    PipeName);

            _startupCoordinator =
                new ApplicationStartupCoordinator(
                    CreateStartupOperations());

            var result =
                await _startupCoordinator.StartAsync(
                    e.Args,
                    _lifetime.Token);

            if (result ==
                ApplicationStartupCoordinator.StartResult.Forwarded)
            {
                _singleInstanceGate.Dispose();
                _singleInstanceGate = null;

                Shutdown();
                return;
            }

            if (result ==
                ApplicationStartupCoordinator.StartResult.DatabaseUnhealthy)
            {
                Shutdown(-1);
            }
        }
        catch (OperationCanceledException)
            when (_lifetime.IsCancellationRequested)
        {
            Shutdown();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"PlayStead n'a pas pu démarrer.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "PlayStead — Erreur de démarrage",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    protected override void OnExit(
        ExitEventArgs e)
    {
        _lifetime.Cancel();

        try
        {
            var coordinator =
                _startupCoordinator;

            if (coordinator is not null)
            {
                Task.Run(
                        () => coordinator.StopAsync(
                            CancellationToken.None))
                    .GetAwaiter()
                    .GetResult();
            }
        }
        finally
        {
            _host?.Dispose();
            _host = null;

            _invocationServer = null;

            _singleInstanceGate?.Dispose();
            _singleInstanceGate = null;

            _lifetime.Dispose();

            base.OnExit(e);
        }
    }

    private ApplicationStartupCoordinator.Operations
        CreateStartupOperations()
    {
        return new ApplicationStartupCoordinator.Operations(
            ParseInvocation: ParseInvocation,
            AcquireSingleInstanceAsync:
                (invocation, cancellationToken) =>
                    RequireSingleInstanceGate()
                        .TryAcquireAsync(
                            invocation,
                            cancellationToken),
            CreateUserDataLayout:
                () =>
                    new WindowsUserDataPathProvider()
                        .Get(),
            EnsureDirectoriesExist:
                layout =>
                    layout.EnsureDirectoriesExist(),
            StartHostAsync:
                StartHostAsync,
            InitializeLocalStateAsync:
                cancellationToken =>
                    GetRequiredService<LocalStartupPipeline>()
                        .InitializeAsync(
                            cancellationToken),
            ShowCachedSnapshotAsync:
                async (snapshot, cancellationToken) =>
                {
                    _ = snapshot;

                    await RunOnUiAsync(
                        async () =>
                        {
                            await GetRequiredService<LibraryViewModel>()
                                .RefreshAsync(
                                    cancellationToken);

                            var window =
                                GetRequiredService<MainWindow>();

                            MainWindow = window;

                            if (!window.IsVisible)
                            {
                                window.Show();
                            }
                        },
                        cancellationToken);
                },
            ShowDatabaseRecoveryRequiredAsync:
                (health, cancellationToken) =>
                    RunOnUiAsync(
                        () =>
                        {
                            MessageBox.Show(
                                $"La base locale PlayStead n'est pas saine.{Environment.NewLine}{Environment.NewLine}{health.Detail}",
                                "PlayStead — Base locale",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

                            return Task.CompletedTask;
                        },
                        cancellationToken),
            BindInvocationReceived:
                BindInvocationReceived,
            BindRescanRequested:
                BindRescanRequested,
            HandleInvocationAsync:
                (invocation, cancellationToken) =>
                    GetRequiredService<ApplicationRuntime>()
                        .HandleInvocationAsync(
                            invocation,
                            cancellationToken),
            RefreshAsync:
                cancellationToken =>
                    GetRequiredService<LocalStartupPipeline>()
                        .RefreshAsync(
                            cancellationToken),
            ApplySnapshotOnUiAsync:
                async (snapshot, cancellationToken) =>
                {
                    _ = snapshot;

                    await RunOnUiAsync(
                        () =>
                            GetRequiredService<LibraryViewModel>()
                                .RefreshAsync(
                                    cancellationToken),
                        cancellationToken);
                },
            StopPipeAsync:
                StopPipeAsync,
            StopHostAsync:
                StopHostAsync,
            ReleaseSingleInstanceAsync:
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    _singleInstanceGate?.Dispose();
                    _singleInstanceGate = null;

                    return Task.CompletedTask;
                });
    }

    private async Task StartHostAsync(
        UserDataLayout layout,
        CancellationToken cancellationToken)
    {
        _host =
            PlaySteadHost.Build(
                layout);

        await _host.StartAsync(
            cancellationToken);
    }

    private void BindInvocationReceived(
        Func<
            AppInvocation,
            CancellationToken,
            Task> handler)
    {
        var server =
            RequireInvocationServer();

        server.InvocationReceived +=
            (_, invocation) =>
            {
                _ = HandleInvocationEventAsync(
                    handler,
                    invocation);
            };

        server.StartAsync(
                _lifetime.Token)
            .GetAwaiter()
            .GetResult();
    }

    private void BindRescanRequested(
        Func<
            CancellationToken,
            Task> handler)
    {
        var window =
            GetRequiredService<MainWindow>();

        window.RescanRequested +=
            (_, _) =>
            {
                _ = HandleRescanEventAsync(
                    handler);
            };
    }

    private async Task HandleInvocationEventAsync(
        Func<
            AppInvocation,
            CancellationToken,
            Task> handler,
        AppInvocation invocation)
    {
        try
        {
            await handler(
                invocation,
                _lifetime.Token);
        }
        catch (OperationCanceledException)
            when (_lifetime.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            await ShowBackgroundOperationErrorAsync(
                "Impossible de traiter la demande d'activation.",
                exception);
        }
    }

    private async Task HandleRescanEventAsync(
        Func<
            CancellationToken,
            Task> handler)
    {
        try
        {
            await handler(
                _lifetime.Token);
        }
        catch (OperationCanceledException)
            when (_lifetime.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            await ShowBackgroundOperationErrorAsync(
                "La nouvelle analyse de la bibliothèque a échoué.",
                exception);
        }
    }

    private Task ShowBackgroundOperationErrorAsync(
        string message,
        Exception exception)
    {
        return RunOnUiAsync(
            () =>
            {
                MessageBox.Show(
                    $"{message}{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                    "PlayStead",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return Task.CompletedTask;
            },
            CancellationToken.None);
    }

    private async Task StopPipeAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_invocationServer is null)
        {
            return;
        }

        await _invocationServer.DisposeAsync();
        _invocationServer = null;
    }

    private async Task StopHostAsync(
        CancellationToken cancellationToken)
    {
        if (_host is null)
        {
            return;
        }

        await _host.StopAsync(
            cancellationToken);

        _host.Dispose();
        _host = null;
    }

    private async Task RunOnUiAsync(
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.CheckAccess())
        {
            await action();
            return;
        }

        var operation =
            Dispatcher.InvokeAsync(
                action,
                DispatcherPriority.Normal,
                cancellationToken);

        var dispatchedTask =
            await operation.Task.ConfigureAwait(false);

        await dispatchedTask.ConfigureAwait(false);
    }

    private T GetRequiredService<T>()
        where T : notnull
    {
        var host =
            _host
            ?? throw new InvalidOperationException(
                "The PlayStead host is not running.");

        return host.Services
            .GetRequiredService<T>();
    }

    private SingleInstanceGate
        RequireSingleInstanceGate() =>
        _singleInstanceGate
        ?? throw new InvalidOperationException(
            "The single-instance gate has not been created.");

    private NamedPipeInvocationServer
        RequireInvocationServer() =>
        _invocationServer
        ?? throw new InvalidOperationException(
            "The invocation server has not been created.");

    private static AppInvocation ParseInvocation(
        IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var deepLink =
            args.FirstOrDefault(
                argument =>
                    argument.StartsWith(
                        "playstead://",
                        StringComparison.OrdinalIgnoreCase));

        return new AppInvocation(
            Activate: true,
            DeepLink: deepLink);
    }
}
