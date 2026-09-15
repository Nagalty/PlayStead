using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
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
    private const string ChildProbeEnvironment = "PLAYSTEAD_B3_APP_EXIT_CHILD_PROBE";
    private const string TargetTest =
        "PlayStead.UI.Tests.Bootstrap.TaskB3DiscoveryAppExitTests.OnExit_pumps_started_nested_ui_apply_until_join_before_host_stop";

    [Fact]
    public async Task Child_probe_rejects_zero_matched_tests_even_when_dotnet_exits_zero()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunProbeInChildProcessAsync("FullyQualifiedName~ThisB3AppExitProbeDoesNotExist"));
    }

    [Fact]
    public async Task OnExit_pumps_started_nested_ui_apply_until_join_before_host_stop()
    {
        if (Environment.GetEnvironmentVariable(ChildProbeEnvironment) != "1")
        {
            await RunProbeInChildProcessAsync();
            return;
        }

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

    private static async Task RunProbeInChildProcessAsync(string? filterOverride = null)
    {
        // WPF Application.Shutdown permanently changes the process-wide singleton.
        // This probe must run in its own testhost so unrelated UI tests can still
        // construct controls after it finishes.
        var project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "PlayStead.UI.Tests.csproj"));
        var results = Directory.CreateTempSubdirectory("playstead-b3-app-exit-");
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Path.GetDirectoryName(project)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in new[]
            {
                "test", project, "--configuration", "Release", "--no-build", "--no-restore",
                "--filter", filterOverride ?? "FullyQualifiedName~" + TargetTest,
                "-m:1", "/nodeReuse:false", "-p:UseSharedCompilation=false",
                "--logger", "trx;LogFileName=app-exit-child.trx",
                "--results-directory", results.FullName
            }) start.ArgumentList.Add(argument);
            start.Environment[ChildProbeEnvironment] = "1";
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw new TimeoutException("B3 app-exit probe child testhost did not exit.");
            }
            var childOutput = await output;
            var childError = await error;
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"B3 app-exit probe child exited {process.ExitCode}.\n{childOutput}\n{childError}");
            ValidateChildTrx(Path.Combine(results.FullName, "app-exit-child.trx"));
        }
        finally
        {
            results.Delete(recursive: true);
        }
    }

    private static void ValidateChildTrx(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException("B3 app-exit child produced no TRX result.");
        var document = XDocument.Load(path);
        var results = document.Descendants().Where(element =>
            element.Name.LocalName == "UnitTestResult").ToArray();
        var counters = document.Descendants().SingleOrDefault(element =>
            element.Name.LocalName == "Counters");
        var valid = results.Length == 1 &&
            (string?)results[0].Attribute("testName") == TargetTest &&
            (string?)results[0].Attribute("outcome") == "Passed" &&
            counters is not null &&
            Counter(counters, "total") == 1 &&
            Counter(counters, "executed") == 1 &&
            Counter(counters, "passed") == 1 &&
            Counter(counters, "failed") == 0 &&
            Counter(counters, "notExecuted") == 0;
        if (!valid)
            throw new InvalidOperationException(
                "B3 app-exit child did not execute and pass exactly one target test.");
    }

    private static int? Counter(XElement counters, string name) =>
        int.TryParse((string?)counters.Attribute(name), out var value) ? value : null;

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
