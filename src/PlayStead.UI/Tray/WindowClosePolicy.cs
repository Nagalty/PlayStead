namespace PlayStead.UI.Tray;

public sealed class WindowClosePolicy
{
    private int _exitRequested;

    public bool IsExitRequested =>
        Volatile.Read(ref _exitRequested) != 0;

    public void RequestExit()
    {
        Interlocked.Exchange(
            ref _exitRequested,
            1);
    }
}
