using PlayStead.Core.Updates;

namespace PlayStead.Providers.Updates;

public interface IGitHubUpdateManifestClient
{
    Task<GitHubUpdateManifest?> GetAsync(Uri manifestUri, string expectedChannel, CancellationToken cancellationToken = default);
}
