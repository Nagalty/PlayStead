using PlayStead.Core.Library;
using PlayStead.Data.Database;
using PlayStead.Platform.Paths;
using PlayStead.Platform.SingleInstance;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class ApplicationStartupCoordinatorTests
{
    [Fact]
    public async Task Forwarded_stops_after_gate_without_touching_user_data_host_database_or_scan()
    {
        var probe = new StartupProbe
        {
            GateResult = SingleInstanceResult.Forwarded
        };

        var sut = new ApplicationStartupCoordinator(
            probe.CreateOperations());

        var result = await sut.StartAsync(
            ["--activate"],
            CancellationToken.None);

        Assert.Equal(
            ApplicationStartupCoordinator.StartResult.Forwarded,
            result);

        Assert.Equal(
            ["parse", "gate"],
            probe.LogSnapshot());

        Assert.Null(sut.BackgroundRefreshTask);
        Assert.False(probe.LayoutCreated);
        Assert.False(probe.DirectoriesEnsured);
        Assert.False(probe.HostStarted);
        Assert.False(probe.DatabaseInitialized);
        Assert.False(probe.MainWindowShown);
        Assert.False(probe.RefreshStarted);
        Assert.False(probe.InvocationBindingInstalled);
        Assert.False(probe.RescanBindingInstalled);
    }

    [Fact]
    public async Task Primary_healthy_startup_shows_cache_before_non_blocking_refresh_and_applies_fresh_snapshot_on_UI()
    {
        var refreshCompletion =
            new TaskCompletionSource<LibrarySnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var probe = new StartupProbe
        {
            GateResult = SingleInstanceResult.Primary,
            Health = new DatabaseHealthResult(
                IsHealthy: true,
                Detail: "ok")
        };

        probe.RefreshBehavior = async cancellationToken =>
        {
            probe.Mark("scan");
            probe.RefreshStarted = true;

            return await refreshCompletion.Task.WaitAsync(
                cancellationToken);
        };

        var sut = new ApplicationStartupCoordinator(
            probe.CreateOperations());

        var startTask = sut.StartAsync(
            ["--activate"],
            CancellationToken.None);

        var result = await startTask.WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.Equal(
            ApplicationStartupCoordinator.StartResult.Started,
            result);

        Assert.True(probe.MainWindowShown);
        Assert.Same(
            probe.CachedSnapshot,
            probe.ShownSnapshot);

        var backgroundRefresh =
            Assert.IsAssignableFrom<Task>(
                sut.BackgroundRefreshTask);

        Assert.False(backgroundRefresh.IsCompleted);

        AssertAppearsBefore(
            probe.LogSnapshot(),
            "parse",
            "gate",
            "layout",
            "dirs",
            "host",
            "initialize",
            "show-cache",
            "scan");

        Assert.DoesNotContain(
            "apply-ui",
            probe.LogSnapshot());

        refreshCompletion.SetResult(
            probe.FreshSnapshot);

        await backgroundRefresh.WaitAsync(
            TimeSpan.FromSeconds(2));

        Assert.Same(
            probe.FreshSnapshot,
            probe.AppliedSnapshot);

        Assert.Equal(
            "apply-ui",
            probe.LogSnapshot()[^1]);
    }

    [Fact]
    public async Task Unhealthy_database_fails_closed_without_main_window_bindings_or_refresh_writes()
    {
        var probe = new StartupProbe
        {
            GateResult = SingleInstanceResult.Primary,
            Health = new DatabaseHealthResult(
                IsHealthy: false,
                Detail: "quick_check failed")
        };

        var sut = new ApplicationStartupCoordinator(
            probe.CreateOperations());

        var result = await sut.StartAsync(
            [],
            CancellationToken.None);

        Assert.Equal(
            ApplicationStartupCoordinator.StartResult.DatabaseUnhealthy,
            result);

        Assert.Equal(
            [
                "parse",
                "gate",
                "layout",
                "dirs",
                "host",
                "initialize",
                "recovery"
            ],
            probe.LogSnapshot());

        Assert.True(probe.RecoveryShown);
        Assert.Same(
            probe.Health,
            probe.RecoveryHealth);

        Assert.False(probe.MainWindowShown);
        Assert.False(probe.RefreshStarted);
        Assert.False(probe.InvocationBindingInstalled);
        Assert.False(probe.RescanBindingInstalled);
        Assert.Null(sut.BackgroundRefreshTask);
    }

    [Fact]
    public async Task Pipe_invocation_is_forwarded_to_the_runtime_handler()
    {
        var probe = new StartupProbe();

        var sut = new ApplicationStartupCoordinator(
            probe.CreateOperations());

        var result = await sut.StartAsync(
            [],
            CancellationToken.None);

        Assert.Equal(
            ApplicationStartupCoordinator.StartResult.Started,
            result);

        Assert.NotNull(
            probe.InvocationReceivedHandler);

        await sut.BackgroundRefreshTask!.WaitAsync(
            TimeSpan.FromSeconds(2));

        probe.ClearLog();

        var invocation = new AppInvocation(
            Activate: false,
            DeepLink: "playstead://ignored-in-0.1");

        await probe.InvocationReceivedHandler!(
            invocation,
            CancellationToken.None);

        Assert.Same(
            invocation,
            probe.LastHandledInvocation);

        Assert.Equal(
            ["handle-invocation"],
            probe.LogSnapshot());
    }

    [Fact]
    public async Task Rescan_request_runs_refresh_and_applies_result_on_UI()
    {
        var probe = new StartupProbe();

        var sut = new ApplicationStartupCoordinator(
            probe.CreateOperations());

        var result = await sut.StartAsync(
            [],
            CancellationToken.None);

        Assert.Equal(
            ApplicationStartupCoordinator.StartResult.Started,
            result);

        Assert.NotNull(
            probe.RescanRequestedHandler);

        await sut.BackgroundRefreshTask!.WaitAsync(
            TimeSpan.FromSeconds(2));

        probe.ClearLog();
        probe.RefreshStarted = false;
        probe.AppliedSnapshot = null;

        await probe.RescanRequestedHandler!(
            CancellationToken.None);

        Assert.True(probe.RefreshStarted);
        Assert.Same(
            probe.FreshSnapshot,
            probe.AppliedSnapshot);

        Assert.Equal(
            ["scan", "apply-ui"],
            probe.LogSnapshot());
    }

    [Fact]
    public async Task StopAsync_stops_pipe_then_host_then_releases_gate_once()
    {
        var probe = new StartupProbe();

        var sut = new ApplicationStartupCoordinator(
            probe.CreateOperations());

        var result = await sut.StartAsync(
            [],
            CancellationToken.None);

        Assert.Equal(
            ApplicationStartupCoordinator.StartResult.Started,
            result);

        await sut.BackgroundRefreshTask!.WaitAsync(
            TimeSpan.FromSeconds(2));

        probe.ClearLog();

        await sut.StopAsync(
            CancellationToken.None);

        await sut.StopAsync(
            CancellationToken.None);

        Assert.Equal(
            [
                "stop-pipe",
                "stop-host",
                "release-gate"
            ],
            probe.LogSnapshot());
    }

    [Fact]
    public void App_xaml_does_not_declare_StartupUri()
    {
        var appXaml = FindRepositoryFile(
            "src",
            "PlayStead.UI",
            "App.xaml");

        var xaml = File.ReadAllText(
            appXaml);

        Assert.False(
            xaml.Contains(
                "StartupUri=",
                StringComparison.OrdinalIgnoreCase),
            "App.xaml must not instantiate MainWindow before the single-instance gate and database startup pipeline.");
    }

    private static void AssertAppearsBefore(
        IReadOnlyList<string> actual,
        params string[] expectedInOrder)
    {
        var previousIndex = -1;

        foreach (var expected in expectedInOrder)
        {
            var index = -1;

            for (var i = previousIndex + 1; i < actual.Count; i++)
            {
                if (string.Equals(
                    actual[i],
                    expected,
                    StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            Assert.True(
                index >= 0,
                $"Expected startup step '{expected}' after index {previousIndex}. Actual: {string.Join(" -> ", actual)}");

            previousIndex = index;
        }
    }

    private static string FindRepositoryFile(
        params string[] relativeParts)
    {
        var starts = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var start in starts)
        {
            var current =
                new DirectoryInfo(
                    Path.GetFullPath(start));

            while (current is not null)
            {
                var candidateParts =
                    new string[relativeParts.Length + 1];

                candidateParts[0] =
                    current.FullName;

                Array.Copy(
                    relativeParts,
                    0,
                    candidateParts,
                    1,
                    relativeParts.Length);

                var candidate =
                    Path.Combine(candidateParts);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {Path.Combine(relativeParts)}");
    }

    private sealed class StartupProbe
    {
        private readonly object _logLock = new();
        private readonly List<string> _log = [];

        public SingleInstanceResult GateResult { get; init; } =
            SingleInstanceResult.Primary;

        public DatabaseHealthResult Health { get; init; } =
            new(
                IsHealthy: true,
                Detail: "ok");

        public LibrarySnapshot CachedSnapshot { get; } =
            CreateEmptySnapshot();

        public LibrarySnapshot FreshSnapshot { get; } =
            CreateEmptySnapshot();

        public Func<CancellationToken, Task<LibrarySnapshot>>? RefreshBehavior
        {
            get;
            set;
        }

        public bool LayoutCreated { get; private set; }
        public bool DirectoriesEnsured { get; private set; }
        public bool HostStarted { get; private set; }
        public bool DatabaseInitialized { get; private set; }
        public bool MainWindowShown { get; private set; }
        public bool RecoveryShown { get; private set; }
        public bool InvocationBindingInstalled { get; private set; }
        public bool RescanBindingInstalled { get; private set; }

        public bool RefreshStarted { get; set; }

        public LibrarySnapshot? ShownSnapshot { get; private set; }
        public LibrarySnapshot? AppliedSnapshot { get; set; }

        public DatabaseHealthResult? RecoveryHealth { get; private set; }

        public AppInvocation? LastHandledInvocation { get; private set; }

        public Func<AppInvocation, CancellationToken, Task>?
            InvocationReceivedHandler
        {
            get;
            private set;
        }

        public Func<CancellationToken, Task>?
            RescanRequestedHandler
        {
            get;
            private set;
        }

        public ApplicationStartupCoordinator.Operations
            CreateOperations()
        {
            return new ApplicationStartupCoordinator.Operations(
                ParseInvocation: args =>
                {
                    Mark("parse");

                    Assert.NotNull(args);

                    return AppInvocation.Default;
                },
                AcquireSingleInstanceAsync:
                    (invocation, cancellationToken) =>
                    {
                        Mark("gate");

                        Assert.NotNull(invocation);

                        return Task.FromResult(
                            GateResult);
                    },
                CreateUserDataLayout: () =>
                {
                    Mark("layout");
                    LayoutCreated = true;

                    var root = Path.Combine(
                        Path.GetTempPath(),
                        "PlayStead.Tests",
                        "StartupCoordinator",
                        Guid.NewGuid().ToString("N"));

                    return UserDataLayout.FromRoot(
                        root);
                },
                EnsureDirectoriesExist: layout =>
                {
                    Mark("dirs");
                    DirectoriesEnsured = true;

                    Assert.NotNull(layout);
                },
                StartHostAsync:
                    (layout, cancellationToken) =>
                    {
                        Mark("host");
                        HostStarted = true;

                        Assert.NotNull(layout);

                        return Task.CompletedTask;
                    },
                InitializeLocalStateAsync:
                    cancellationToken =>
                    {
                        Mark("initialize");
                        DatabaseInitialized = true;

                        return Task.FromResult(
                            new LocalStartupState(
                                Health,
                                CachedSnapshot));
                    },
                ShowCachedSnapshotAsync:
                    (snapshot, cancellationToken) =>
                    {
                        Mark("show-cache");
                        MainWindowShown = true;
                        ShownSnapshot = snapshot;

                        return Task.CompletedTask;
                    },
                ShowDatabaseRecoveryRequiredAsync:
                    (health, cancellationToken) =>
                    {
                        Mark("recovery");
                        RecoveryShown = true;
                        RecoveryHealth = health;

                        return Task.CompletedTask;
                    },
                BindInvocationReceived: handler =>
                {
                    Mark("bind-pipe");
                    InvocationBindingInstalled = true;
                    InvocationReceivedHandler = handler;
                },
                BindRescanRequested: handler =>
                {
                    Mark("bind-rescan");
                    RescanBindingInstalled = true;
                    RescanRequestedHandler = handler;
                },
                HandleInvocationAsync:
                    (invocation, cancellationToken) =>
                    {
                        Mark("handle-invocation");
                        LastHandledInvocation = invocation;

                        return Task.CompletedTask;
                    },
                RefreshAsync: RefreshAsync,
                ApplySnapshotOnUiAsync:
                    (snapshot, cancellationToken) =>
                    {
                        Mark("apply-ui");
                        AppliedSnapshot = snapshot;

                        return Task.CompletedTask;
                    },
                StopPipeAsync:
                    cancellationToken =>
                    {
                        Mark("stop-pipe");

                        return Task.CompletedTask;
                    },
                StopHostAsync:
                    cancellationToken =>
                    {
                        Mark("stop-host");

                        return Task.CompletedTask;
                    },
                ReleaseSingleInstanceAsync:
                    cancellationToken =>
                    {
                        Mark("release-gate");

                        return Task.CompletedTask;
                    });
        }

        public void Mark(
            string step)
        {
            lock (_logLock)
            {
                _log.Add(step);
            }
        }

        public string[] LogSnapshot()
        {
            lock (_logLock)
            {
                return [.. _log];
            }
        }

        public void ClearLog()
        {
            lock (_logLock)
            {
                _log.Clear();
            }
        }

        private Task<LibrarySnapshot> RefreshAsync(
            CancellationToken cancellationToken)
        {
            if (RefreshBehavior is not null)
            {
                return RefreshBehavior(
                    cancellationToken);
            }

            Mark("scan");
            RefreshStarted = true;

            return Task.FromResult(
                FreshSnapshot);
        }

        private static LibrarySnapshot CreateEmptySnapshot() =>
            new(
                Array.Empty<LogicalGame>(),
                Array.Empty<GameInstallation>());
    }
}
