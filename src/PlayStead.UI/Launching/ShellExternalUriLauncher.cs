using System.Diagnostics;

namespace PlayStead.UI.Launching;

public sealed class ShellExternalUriLauncher :
    IExternalUriLauncher
{
    private readonly Action<ProcessStartInfo>
        _start;

    public ShellExternalUriLauncher(
        Action<ProcessStartInfo> start)
    {
        ArgumentNullException.ThrowIfNull(
            start);

        _start =
            start;
    }

    public void Open(
        Uri uri)
    {
        ArgumentNullException.ThrowIfNull(
            uri);

        _start(
            new ProcessStartInfo
            {
                FileName =
                    uri.AbsoluteUri,
                UseShellExecute =
                    true
            });
    }
}
