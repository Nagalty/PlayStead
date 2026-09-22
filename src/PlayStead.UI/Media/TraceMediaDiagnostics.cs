using System.Diagnostics;
using PlayStead.Core.Media;

namespace PlayStead.UI.Media;

public sealed class TraceMediaDiagnostics : IMediaDiagnostics
{
    public void Report(MediaResolutionEvent mediaEvent)
    {
        ArgumentNullException.ThrowIfNull(mediaEvent);

        try
        {
            Trace.WriteLine(
                $"Media {mediaEvent.Kind} {mediaEvent.Provider} {mediaEvent.ProviderGameId} {mediaEvent.AssetType}");
        }
        catch
        {
            // Media diagnostics are observational and must not affect resolution.
        }
    }
}
