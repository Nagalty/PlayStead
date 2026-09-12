using System.Collections.Concurrent;
using PlayStead.Platform.SingleInstance;

namespace PlayStead.Platform.Tests.SingleInstance;

public sealed class NamedPipeInvocationServerSynchronizationTests
{
    [Fact]
    public async Task DisposeAsync_completes_without_pumping_the_callers_synchronization_context()
    {
        var pipeName =
            $"PlayStead.Tests.SyncContext.{Guid.NewGuid():N}";

        var probeSource =
            new TaskCompletionSource<DisposeProbe>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var threadFinished =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var thread =
            new Thread(
                () =>
                {
                    var context =
                        new QueueingSynchronizationContext();

                    SynchronizationContext.SetSynchronizationContext(
                        context);

                    NamedPipeInvocationServer? server = null;

                    try
                    {
                        server =
                            new NamedPipeInvocationServer(
                                pipeName);

                        server.StartAsync(
                                CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();

                        var disposeTask =
                            server.DisposeAsync()
                                .AsTask();

                        var completedWithoutPumping =
                            disposeTask.Wait(
                                TimeSpan.FromSeconds(1));

                        probeSource.TrySetResult(
                            new DisposeProbe(
                                completedWithoutPumping,
                                context.PostCount));

                        if (!completedWithoutPumping)
                        {
                            context.DrainUntil(
                                disposeTask,
                                TimeSpan.FromSeconds(3));
                        }
                    }
                    catch (Exception exception)
                    {
                        probeSource.TrySetException(
                            exception);
                    }
                    finally
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            null);

                        threadFinished.TrySetResult();
                    }
                })
            {
                IsBackground = true,
                Name = "PlayStead pipe synchronization-context probe"
            };

        thread.Start();

        var probe =
            await probeSource.Task.WaitAsync(
                TimeSpan.FromSeconds(5));

        await threadFinished.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.True(
            probe.CompletedWithoutPumping,
            $"NamedPipeInvocationServer.DisposeAsync required the caller's synchronization context to be pumped. Posts observed before cleanup: {probe.PostCount}.");
    }

    private sealed record DisposeProbe(
        bool CompletedWithoutPumping,
        int PostCount);

    private sealed class QueueingSynchronizationContext
        : SynchronizationContext
    {
        private readonly ConcurrentQueue<
            (SendOrPostCallback Callback, object? State)> _queue =
            new();

        private int _postCount;

        public int PostCount =>
            Volatile.Read(
                ref _postCount);

        public override void Post(
            SendOrPostCallback d,
            object? state)
        {
            Interlocked.Increment(
                ref _postCount);

            _queue.Enqueue(
                (d, state));
        }

        public void DrainUntil(
            Task task,
            TimeSpan timeout)
        {
            var stopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            while (!task.IsCompleted
                   && stopwatch.Elapsed < timeout)
            {
                while (_queue.TryDequeue(
                           out var work))
                {
                    work.Callback(
                        work.State);
                }

                if (!task.IsCompleted)
                {
                    Thread.Sleep(10);
                }
            }

            while (_queue.TryDequeue(
                       out var remaining))
            {
                remaining.Callback(
                    remaining.State);
            }
        }
    }
}
