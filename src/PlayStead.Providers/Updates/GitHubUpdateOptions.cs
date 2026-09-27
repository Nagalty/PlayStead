namespace PlayStead.Providers.Updates;

public sealed record GitHubUpdateOptions(
    string? ManifestUrl = null,
    string? ExpectedReleaseChannel = null,
    TimeSpan? Timeout = null);
