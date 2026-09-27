namespace PlayStead.Core.ProviderInstallUpdate;

public enum ProviderInstallUpdateStatus
{
    Unknown = 0,
    UpToDate = 1,
    UpdateAvailable = 2,
    Downloading = 3,
    Staging = 4,
    VersionMismatch = 5
}
