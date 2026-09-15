using System.ComponentModel;

namespace PlayStead.Core.Sessions;

/// <summary>A classified Windows enumeration failure after the runtime marked a capture gap.</summary>
public sealed class ProcessCaptureUnavailableException : Exception
{
    public ProcessCaptureUnavailableException(Win32Exception cause)
        : base("Windows process capture is unavailable for this cycle.", cause)
    {
    }
}
