namespace PlayStead.Core.Updates;

public sealed record GitHubUpdateManifest(
    string Version,
    DistributionChannel Channel,
    Uri PackageUri,
    string Sha256,
    Uri? ReleaseNotesUri = null,
    DateTimeOffset? PublishedAt = null)
{
    public string ReleaseChannel { get; init; } = "stable";

    public bool IsValid(out string? error)
    {
        if (Channel != DistributionChannel.GitHub)
        {
            error = "Manifest channel must be GitHub.";
            return false;
        }

        if (!SemanticVersion.TryParse(Version, out _))
        {
            error = "Manifest version is not a valid semantic version.";
            return false;
        }

        if (PackageUri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Package URI must use HTTPS.";
            return false;
        }

        if (ReleaseNotesUri is not null && ReleaseNotesUri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Release notes URI must use HTTPS.";
            return false;
        }

        if (Sha256.Length != 64 || !Sha256.All(UriHex))
        {
            error = "SHA-256 must contain 64 hexadecimal characters.";
            return false;
        }

        error = null;
        return true;

        static bool UriHex(char value) =>
            value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }
}
