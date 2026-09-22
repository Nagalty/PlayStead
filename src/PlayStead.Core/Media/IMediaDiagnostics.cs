namespace PlayStead.Core.Media;

public interface IMediaDiagnostics
{
    void Report(MediaResolutionEvent mediaEvent);
}
