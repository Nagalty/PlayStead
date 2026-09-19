using System.Diagnostics;
using System.IO;

namespace PlayStead.UI.Bootstrap;

public sealed class StartupTraceFileSink : IDisposable
{
    private TextWriterTraceListener? _listener;

    public string? LogPath { get; private set; }

    public bool TryInstall(string? localAppDataPath = null)
    {
        if (_listener is not null)
            return true;

        TextWriterTraceListener? listener = null;
        try
        {
            var localAppData = localAppDataPath
                ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDirectory = Path.Combine(localAppData, "PlayStead", "Logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "startup-trace.log");
            if (File.Exists(logPath))
                File.Delete(logPath);

            listener = new TextWriterTraceListener(logPath);
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;
            _listener = listener;
            LogPath = logPath;
            return true;
        }
        catch
        {
            if (listener is not null)
            {
                Trace.Listeners.Remove(listener);
                listener.Dispose();
            }
            return false;
        }
    }

    public void Dispose()
    {
        if (_listener is null)
            return;

        Trace.Listeners.Remove(_listener);
        _listener.Dispose();
        _listener = null;
    }
}
