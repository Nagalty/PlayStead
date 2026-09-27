namespace PlayStead.Core.Updates;

public sealed record AppUpdateState(
    AppUpdateStatus Status,
    DistributionChannel Channel,
    string CurrentVersion,
    string? AvailableVersion = null,
    Uri? ReleaseNotesUri = null,
    Uri? ActionUri = null,
    AppUpdateActionKind Action = AppUpdateActionKind.None,
    string? Error = null,
    string? LocalPackagePath = null,
    string? ExpectedSha256 = null,
    long BytesReceived = 0,
    long? TotalBytes = null)
{
    public static AppUpdateState Unknown(
        DistributionChannel channel,
        string currentVersion) =>
        new(AppUpdateStatus.Unknown, channel, currentVersion);
}
