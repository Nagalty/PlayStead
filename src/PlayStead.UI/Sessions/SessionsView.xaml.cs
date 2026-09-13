using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PlayStead.UI.Sessions;

public partial class SessionsView :
    UserControl
{
    private readonly DispatcherTimer _liveTimer;

    public SessionsView()
    {
        InitializeComponent();

        _liveTimer =
            new DispatcherTimer(
                DispatcherPriority.Background)
            {
                Interval =
                    TimeSpan.FromSeconds(1)
            };

        _liveTimer.Tick +=
            LiveTimer_OnTick;

        Loaded +=
            SessionsView_OnLoaded;

        Unloaded +=
            SessionsView_OnUnloaded;
    }

    private async void SessionsView_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            if (DataContext is
                SessionViewModel viewModel)
            {
                await viewModel.RefreshAsync(
                    CancellationToken.None);
            }
        }
        catch (Exception)
        {
            // A session-history read failure must not crash
            // the WPF dispatcher or prevent the page from opening.
        }
        finally
        {
            _liveTimer.Start();
        }
    }

    private void SessionsView_OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        _liveTimer.Stop();
    }

    private void LiveTimer_OnTick(
        object? sender,
        EventArgs e)
    {
        RefreshLive();
    }

    private async void RecentSessionDetailsButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button
            {
                DataContext:
                    RecentSessionItemViewModel item
            })
        {
            return;
        }

        if (DataContext is
            SessionViewModel viewModel)
        {
            await viewModel.SelectRecentSessionAsync(
                item.SessionId,
                CancellationToken.None);
        }
    }

    private void RefreshLive()
    {
        if (DataContext is
            SessionViewModel viewModel)
        {
            viewModel.RefreshLive();
        }
    }
}
