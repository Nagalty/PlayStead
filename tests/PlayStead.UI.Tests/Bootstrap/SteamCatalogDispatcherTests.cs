using System.Collections.Concurrent;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Providers.Steam;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class SteamCatalogDispatcherTests
{
    [Fact]
    public void Blocking_catalog_work_runs_off_ui_context_and_progress_returns_to_it()
    {
        var uiThread = Environment.CurrentManagedThreadId;
        using var context = new PumpingSynchronizationContext(uiThread);
        var gameId = GameId.New();
        var source = new RecordingSource(
            new CanonicalCatalogImportItem(
                gameId,
                "1203620",
                "Enshrouded",
                "Keen Games GmbH",
                "Keen Games GmbH",
                DateTimeOffset.UtcNow));
        var writer = new RecordingWriter();
        var bootstrapper = new SteamLocalCatalogBootstrapper(source, writer);
        var progress = new StartupProgressState();
        var progressThread = 0;
        progress.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(StartupProgressState.Current) && progress.Current == 1)
                progressThread = Environment.CurrentManagedThreadId;
        };

        context.Run(async () =>
            await LocalStartupPipeline.RunSteamCatalogBootstrapAsync(
                bootstrapper,
                EmptySnapshot(),
                DateTimeOffset.UtcNow,
                progress,
                context,
                CancellationToken.None));

        Assert.Equal(1, source.CallCount);
        Assert.NotEqual(uiThread, source.ThreadId);
        Assert.NotEqual(uiThread, writer.ThreadId);
        Assert.Equal(uiThread, progressThread);
        Assert.Equal(1, progress.Current);
        Assert.Equal(1, progress.Total);
    }

    private static LibrarySnapshot EmptySnapshot() =>
        new([], []);

    private sealed class RecordingSource(CanonicalCatalogImportItem item)
        : ISteamLocalCatalogImportSource
    {
        public int CallCount { get; private set; }
        public int ThreadId { get; private set; }

        public IReadOnlyList<CanonicalCatalogImportItem> CreateItems(
            LibrarySnapshot snapshot,
            DateTimeOffset observedAtUtc)
        {
            CallCount++;
            ThreadId = Environment.CurrentManagedThreadId;
            return [item];
        }
    }

    private sealed class RecordingWriter : ICanonicalCatalogWriter
    {
        public int ThreadId { get; private set; }

        public Task ImportSteamAsync(
            IReadOnlyCollection<CanonicalCatalogImportItem> items,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Single(items);
            ThreadId = Environment.CurrentManagedThreadId;
            return Task.CompletedTask;
        }
    }

    private sealed class PumpingSynchronizationContext(int ownerThreadId) : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<WorkItem> _queue = [];

        public override void Post(SendOrPostCallback callback, object? state) =>
            _queue.Add(new(callback, state, null));

        public override void Send(SendOrPostCallback callback, object? state)
        {
            if (Environment.CurrentManagedThreadId == ownerThreadId)
            {
                callback(state);
                return;
            }

            using var completed = new ManualResetEventSlim();
            _queue.Add(new(callback, state, completed));
            completed.Wait();
        }

        public void Run(Func<Task> operation)
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try
            {
                var task = operation();
                while (!task.IsCompleted)
                {
                    if (_queue.TryTake(out var work, TimeSpan.FromSeconds(2)))
                        Execute(work);
                }

                while (_queue.TryTake(out var remaining))
                    Execute(remaining);

                task.GetAwaiter().GetResult();
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }

        public void Dispose() => _queue.Dispose();

        private static void Execute(WorkItem work)
        {
            try
            {
                work.Callback(work.State);
            }
            finally
            {
                work.Completed?.Set();
            }
        }

        private sealed record WorkItem(
            SendOrPostCallback Callback,
            object? State,
            ManualResetEventSlim? Completed);
    }
}
