using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using PlayStead.Core.Sessions;
using PlayStead.Core.ProviderInstallUpdate;
using PlayStead.UI.Sessions;
using PlayStead.UI.SingleInstance;
using Forms = System.Windows.Forms;

namespace PlayStead.UI.Tray;

public sealed class TrayIconService : IDisposable, IWindowsSilentNotificationSink
{
    private readonly IWindowActivator _windowActivator;
    private readonly WindowClosePolicy _closePolicy;
    private readonly SessionMonitor _sessionMonitor;

    private Forms.NotifyIcon? _notifyIcon;
    private Icon? _officialIcon;
    private MemoryStream? _officialIconStream;
    private Forms.ContextMenuStrip? _contextMenu;
    private int _lastSessionCount;
    private bool _started;
    private bool _disposed;

    public TrayIconService(
        IWindowActivator windowActivator,
        WindowClosePolicy closePolicy,
        SessionMonitor sessionMonitor)
    {
        ArgumentNullException.ThrowIfNull(windowActivator);
        ArgumentNullException.ThrowIfNull(closePolicy);
        ArgumentNullException.ThrowIfNull(sessionMonitor);

        _windowActivator = windowActivator;
        _closePolicy = closePolicy;
        _sessionMonitor = sessionMonitor;
    }

    public event EventHandler? ExitRequested;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        if (_started)
        {
            return;
        }

        _started = true;

        var openItem =
            new Forms.ToolStripMenuItem(
                "Ouvrir PlayStead");

        openItem.Click +=
            OpenItem_OnClick;

        var quitItem =
            new Forms.ToolStripMenuItem(
                "Quitter PlayStead");

        quitItem.Click +=
            QuitItem_OnClick;

        _contextMenu =
            new Forms.ContextMenuStrip();

        _contextMenu.Items.Add(
            openItem);

        _contextMenu.Items.Add(
            new Forms.ToolStripSeparator());

        _contextMenu.Items.Add(
            quitItem);

        _officialIconStream = LoadOfficialIconStream();
        _officialIcon = new Icon(_officialIconStream);

        _notifyIcon =
            new Forms.NotifyIcon
            {
                ContextMenuStrip = _contextMenu,
                Icon = _officialIcon,
                Text = TrayStatusText.Format(
                    _lastSessionCount),
                Visible = true
            };

        _notifyIcon.DoubleClick +=
            NotifyIcon_OnDoubleClick;

        _sessionMonitor.SnapshotUpdated +=
            SessionMonitor_OnSnapshotUpdated;

        if (_sessionMonitor.LatestSnapshot is not null)
        {
            UpdateSessionCount(
                _sessionMonitor
                    .LatestSnapshot
                    .ActiveSessions
                    .Count);
        }
    }

    public Task OpenAsync(
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        return _windowActivator.ActivateAsync(
            cancellationToken);
    }

    public Task ShowAsync(string title, string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _notifyIcon?.ShowBalloonTip(5000, title, message, Forms.ToolTipIcon.None);
        return Task.CompletedTask;
    }

    public void RequestExit()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        _closePolicy.RequestExit();

        ExitRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    public void UpdateSessionCount(
        int activeSessionCount)
    {
        if (activeSessionCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activeSessionCount),
                activeSessionCount,
                "Active session count cannot be negative.");
        }

        _lastSessionCount =
            activeSessionCount;

        var text =
            TrayStatusText.Format(
                activeSessionCount);

        var dispatcher =
            Application.Current?.Dispatcher;

        if (dispatcher is not null &&
            !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(
                () => ApplyTooltip(text),
                DispatcherPriority.Normal);

            return;
        }

        ApplyTooltip(
            text);
    }

    private async void OpenItem_OnClick(
        object? sender,
        EventArgs e)
    {
        await TryOpenAsync();
    }

    private async void NotifyIcon_OnDoubleClick(
        object? sender,
        EventArgs e)
    {
        await TryOpenAsync();
    }

    private void QuitItem_OnClick(
        object? sender,
        EventArgs e)
    {
        RequestExit();
    }

    private void SessionMonitor_OnSnapshotUpdated(
        SessionRuntimeSnapshot snapshot)
    {
        UpdateSessionCount(
            snapshot.ActiveSessions.Count);
    }

    private async Task TryOpenAsync()
    {
        try
        {
            await OpenAsync(
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            await ShowOpenErrorAsync(
                exception);
        }
    }

    private static Task ShowOpenErrorAsync(
        Exception exception)
    {
        var dispatcher =
            Application.Current?.Dispatcher;

        if (dispatcher is null)
        {
            return Task.CompletedTask;
        }

        if (dispatcher.CheckAccess())
        {
            MessageBox.Show(
                $"Impossible d'ouvrir PlayStead.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "PlayStead",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(
                () =>
                    MessageBox.Show(
                        $"Impossible d'ouvrir PlayStead.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                        "PlayStead",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning),
                DispatcherPriority.Normal)
            .Task;
    }

    private void ApplyTooltip(
        string text)
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Text =
                text;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_started)
        {
            _sessionMonitor.SnapshotUpdated -=
                SessionMonitor_OnSnapshotUpdated;
        }

        if (_notifyIcon is not null)
        {
            _notifyIcon.DoubleClick -=
                NotifyIcon_OnDoubleClick;

            _notifyIcon.Visible =
                false;

            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _contextMenu?.Dispose();
        _contextMenu = null;

        _officialIcon?.Dispose();
        _officialIcon = null;
        _officialIconStream?.Dispose();
        _officialIconStream = null;
    }

    private static MemoryStream LoadOfficialIconStream()
    {
        var resource = Application.GetResourceStream(
            new Uri(
                "/PlayStead.UI;component/Assets/Branding/PlayStead.ico",
                UriKind.Relative));

        if (resource?.Stream is null)
        {
            throw new InvalidOperationException(
                "The official PlayStead application icon resource could not be loaded.");
        }

        using (resource.Stream)
        {
            var bytes = new MemoryStream();
            resource.Stream.CopyTo(bytes);
            bytes.Position = 0;
            return bytes;
        }
    }
}
