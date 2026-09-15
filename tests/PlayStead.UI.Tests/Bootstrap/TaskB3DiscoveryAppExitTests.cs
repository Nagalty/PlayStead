using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using PlayStead.Core.Library;
using PlayStead.Data.Database;
using PlayStead.Platform.Paths;
using PlayStead.Platform.SingleInstance;
using PlayStead.UI;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class TaskB3DiscoveryAppExitTests
{
    [Fact]
    public async Task OnExit_pumps_started_nested_ui_apply_until_join_before_host_stop()
    {
        var ready = new TaskCompletionSource<(Dispatcher Dispatcher, ExitProbeApp App)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var uiStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var uiFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pipeStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateRescanRejected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshot = new LibrarySnapshot([], []);
        Dispatcher? dispatcher = null;
        Func<CancellationToken, Task>? rescan = null;
        var applies = 0;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new ExitProbeApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                dispatcher = app.Dispatcher;
                var operations = new ApplicationStartupCoordinator.Operations(
                    _ => new AppInvocation(true, null),
                    (_, _) => Task.FromResult(SingleInstanceResult.Primary),
                    () => UserDataLayout.FromRoot(Path.GetTempPath()), _ => { },
                    (_, _) => Task.CompletedTask,
                    _ => Task.FromResult(new LocalStartupState(new DatabaseHealthResult(true, "ok"), snapshot)),
                    _ => Task.CompletedTask, (_, _) => Task.CompletedTask,
                    (_, _) => Task.CompletedTask, _ => { },
                    handler => rescan = handler,
                    (_, _) => Task.CompletedTask,
                    _ => Task.FromResult(snapshot),
                    async (_, _) =>
                    {
                        if (Interlocked.Increment(ref applies) == 1) return;
                        var operation = dispatcher.InvokeAsync(async () =>
                        {
                            uiStarted.TrySetResult();
                            await releaseUi.Task.ConfigureAwait(false);
                            await dispatcher.InvokeAsync(() => uiFinished.TrySetResult(),
                                DispatcherPriority.Normal).Task;
                        }, DispatcherPriority.Normal);
                        var nested = await operation.Task;
                        await nested;
                    },
                    _ => { pipeStopped.TrySetResult(); return Task.CompletedTask; },
                    _ => { hostStopped.TrySetResult(); return Task.CompletedTask; },
                    _ => Task.CompletedTask);
                var coordinator = new ApplicationStartupCoordinator(operations);
                typeof(App).GetField("_startupCoordinator",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, coordinator);
                coordinator.StartAsync([], CancellationToken.None).GetAwaiter().GetResult();
                coordinator.BackgroundRefreshTask!.GetAwaiter().GetResult();
                dispatcher.BeginInvoke(new Action(() => { _ = rescan!(CancellationToken.None); }),
                    DispatcherPriority.Normal);
                ready.TrySetResult((dispatcher, app));
                app.Run();
                exited.TrySetResult();
            }
            catch (Exception error)
            {
                ready.TrySetException(error);
                exited.TrySetException(error);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        try
        {
            var (uiDispatcher, probe) = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await uiStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            _ = uiDispatcher.BeginInvoke(new Action(probe.Shutdown), DispatcherPriority.Normal);
            await pipeStopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(hostStopped.Task.IsCompleted);
            _ = uiDispatcher.BeginInvoke(new Action(() =>
            {
                lateRescanRejected.TrySetResult(rescan!(CancellationToken.None).IsCanceled);
                probe.Shutdown();
            }), DispatcherPriority.Normal);
            Assert.True(await lateRescanRejected.Task.WaitAsync(TimeSpan.FromSeconds(3)));
            releaseUi.TrySetResult();
            await exited.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(probe.DispatcherShutdownStartedAtExit);
            Assert.True(uiFinished.Task.IsCompleted);
            Assert.True(hostStopped.Task.IsCompleted);
            Assert.Equal(1, probe.ExitCount);
        }
        finally
        {
            releaseUi.TrySetResult();
            if (!exited.Task.IsCompleted)
            {
                thread.Interrupt();
                thread.Join(TimeSpan.FromSeconds(1));
            }
        }
    }

    private sealed class ExitProbeApp : App
    {
        public bool DispatcherShutdownStartedAtExit { get; private set; }
        public int ExitCount { get; private set; }
        protected override void OnStartup(StartupEventArgs e) { }
        protected override void OnExit(ExitEventArgs e)
        {
            ExitCount++;
            DispatcherShutdownStartedAtExit = Dispatcher.HasShutdownStarted;
            base.OnExit(e);
        }
    }
}
