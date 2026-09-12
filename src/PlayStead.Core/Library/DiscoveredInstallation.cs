namespace PlayStead.Core.Library;

public sealed record DiscoveredInstallation(
    ProviderKind Provider,
    string ExternalId,
    string Title,
    string InstallPath,
    long? InstalledSizeBytes,
    DateTimeOffset ObservedAtUtc)
{
    public static DiscoveredInstallation Create(
        ProviderKind provider,
        string externalId,
        string title,
        string installPath,
        long? installedSizeBytes,
        DateTimeOffset observedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(installPath);

        if (installedSizeBytes is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(installedSizeBytes));
        }

        return new DiscoveredInstallation(
            provider,
            externalId.Trim(),
            title.Trim(),
            Path.GetFullPath(installPath),
            installedSizeBytes,
            observedAtUtc);
    }
}
