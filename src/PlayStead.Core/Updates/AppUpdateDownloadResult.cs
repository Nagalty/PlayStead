namespace PlayStead.Core.Updates;

public sealed record AppUpdateDownloadResult(
    string LocalPackagePath,
    long BytesReceived,
    long? TotalBytes);
