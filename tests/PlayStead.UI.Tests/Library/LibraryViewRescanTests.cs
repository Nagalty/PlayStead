using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryViewRescanTests
{
    [Fact]
    public void Rescan_button_raises_RescanRequested_exactly_once()
    {
        RunSta(() =>
        {
            var view = new LibraryView();

            var count = 0;
            view.RescanRequested += (_, _) => count++;

            var button = Assert.IsType<Button>(
                view.FindName("RescanButton"));

            button.RaiseEvent(
                new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(1, count);

            return 0;
        });
    }

    private static T RunSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { error = ex; }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return result!;
    }
}
