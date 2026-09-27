namespace PlayStead.Core.Updates;

public enum AppUpdateStatus
{
    Unknown = 0,
    Checking = 1,
    UpToDate = 2,
    UpdateAvailable = 3,
    Error = 4,
    Downloading = 5,
    ReadyToInstall = 6
}
