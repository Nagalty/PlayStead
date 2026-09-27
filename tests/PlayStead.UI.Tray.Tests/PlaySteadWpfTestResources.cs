using System.Windows;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;

namespace PlayStead.UI.Tests.TestSupport;

[CollectionDefinition("PlayStead WPF application state", DisableParallelization = true)]
public sealed class PlaySteadWpfApplicationCollection
{
    public const string Name = "PlayStead WPF application state";
}

/// <summary>
/// Loads the exact shared dictionaries merged by production App.xaml while a
/// WPF test is using its STA. The lock covers the view lifetime so parallel
/// tests cannot remove dictionaries another view is still resolving.
/// </summary>
internal static class PlaySteadWpfTestResources
{
    private static readonly object ResourceGate = new();
    private static readonly Lazy<SharedStaDispatcher> SharedDispatcher =
        new(CreateSharedDispatcher, LazyThreadSafetyMode.ExecutionAndPublication);

    public static T Run<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = SharedDispatcher.Value.Dispatcher;
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            throw new InvalidOperationException("The shared STA dispatcher was shut down by a test.");
        return dispatcher.Invoke(() =>
        {
            using var resources = Install();
            return action();
        }, DispatcherPriority.Send);
    }

    public static void Run(Action action) => Run(() =>
    {
        action();
        return true;
    });

    public static void RunOnCurrentSta(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var resources = Install();
        action();
    }

    public static Task RunAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = SharedDispatcher.Value.Dispatcher;
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            throw new InvalidOperationException("The shared STA dispatcher was shut down by a test.");
        return dispatcher.InvokeAsync(async () =>
        {
            using var resources = Install();
            await action();
        }, DispatcherPriority.Send).Task.Unwrap();
    }

    private static IDisposable Install()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("Shared WPF resources must be installed on an STA thread.");

        Monitor.Enter(ResourceGate);
        try
        {
            var application = WpfApplication.Current ?? new WpfApplication
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var added = new List<ResourceDictionary>();
            AddIfMissing(application, "PlaySteadTokens.xaml", added);
            AddIfMissing(application, "PlaySteadControls.xaml", added);
            return new ResourceScope(application, added);
        }
        catch
        {
            Monitor.Exit(ResourceGate);
            throw;
        }
    }

    public static void ShowAndPumpLoaded(Window window, FrameworkElement element)
    {
        var dispatcher = element.Dispatcher;
        var loaded = element.IsLoaded;
        var frame = new DispatcherFrame();

        RoutedEventHandler handler = (_, _) =>
        {
            loaded = true;
            frame.Continue = false;
        };

        element.Loaded += handler;
        try
        {
            window.Show();
            if (!loaded)
            {
                var timeout = new DispatcherTimer(DispatcherPriority.Send, dispatcher)
                {
                    Interval = TimeSpan.FromSeconds(10)
                };
                timeout.Tick += (_, _) =>
                {
                    timeout.Stop();
                    frame.Continue = false;
                };
                timeout.Start();
                Dispatcher.PushFrame(frame);
                timeout.Stop();
            }
        }
        finally
        {
            element.Loaded -= handler;
        }

        if (!loaded && !element.IsLoaded)
            throw new TimeoutException("The WPF Loaded event was not dispatched for the test element.");
    }

    private static void AddIfMissing(
        WpfApplication application,
        string dictionaryName,
        ICollection<ResourceDictionary> added)
    {
        var source = new Uri($"/PlayStead.UI;component/Themes/{dictionaryName}", UriKind.Relative);
        var existing = application.Resources.MergedDictionaries.Any(dictionary =>
            dictionary.Source is { } current &&
            string.Equals(current.OriginalString, source.OriginalString, StringComparison.OrdinalIgnoreCase));
        if (existing)
            return;

        var dictionary = (ResourceDictionary)WpfApplication.LoadComponent(source);
        application.Resources.MergedDictionaries.Add(dictionary);
        added.Add(dictionary);
    }

    private static SharedStaDispatcher CreateSharedDispatcher()
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                _ = WpfApplication.Current ?? new WpfApplication
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                var dispatcher = Dispatcher.CurrentDispatcher;
                ready.TrySetResult(dispatcher);
                Dispatcher.Run();
            }
            catch (Exception exception)
            {
                ready.TrySetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "PlayStead UI test dispatcher"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return new SharedStaDispatcher(ready.Task.GetAwaiter().GetResult(), thread);
    }

    private sealed record SharedStaDispatcher(Dispatcher Dispatcher, Thread Thread);

    private sealed class ResourceScope(
        WpfApplication application,
        IReadOnlyList<ResourceDictionary> added) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                for (var index = added.Count - 1; index >= 0; index--)
                    application.Resources.MergedDictionaries.Remove(added[index]);
            }
            finally
            {
                _disposed = true;
                Monitor.Exit(ResourceGate);
            }
        }
    }
}
