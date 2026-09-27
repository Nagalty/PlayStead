namespace PlayStead.Core.Updates;

public interface IAppUpdatePackageDownloader
{
    Task<AppUpdateDownloadResult> DownloadAsync(
        Uri packageUri,
        string expectedSha256,
        string version,
        IProgress<AppUpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
