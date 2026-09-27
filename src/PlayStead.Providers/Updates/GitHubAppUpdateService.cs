using PlayStead.Core.Updates;

namespace PlayStead.Providers.Updates;

public sealed class GitHubAppUpdateService : IAppUpdateService
{
    private readonly string _currentVersion;
    private readonly Func<CancellationToken, Task<GitHubUpdateManifest?>>? _manifestSource;
    private readonly GitHubUpdateOptions _options;
    private readonly IGitHubUpdateManifestClient? _client;

    public GitHubAppUpdateService(
        string currentVersion,
        Func<CancellationToken, Task<GitHubUpdateManifest?>>? manifestSource = null,
        GitHubUpdateOptions? options = null,
        IGitHubUpdateManifestClient? client = null)
    {
        _currentVersion = currentVersion;
        _manifestSource = manifestSource;
        _options = options ?? new GitHubUpdateOptions();
        _client = client;
        Current = AppUpdateState.Unknown(Channel, currentVersion);
    }

    public DistributionChannel Channel => DistributionChannel.GitHub;
    public AppUpdateState Current { get; private set; }

    public async Task<AppUpdateState> CheckAsync(CancellationToken cancellationToken = default)
    {
        var expectedChannel = ResolveExpectedChannel();
        if (_manifestSource is null && (_client is null || string.IsNullOrWhiteSpace(_options.ManifestUrl)))
            return Current = AppUpdateState.Unknown(Channel, _currentVersion);

        try
        {
            var manifest = _manifestSource is not null
                ? await _manifestSource(cancellationToken).ConfigureAwait(false)
                : await _client!.GetAsync(new Uri(_options.ManifestUrl!, UriKind.Absolute), expectedChannel, cancellationToken).ConfigureAwait(false);
            if (manifest is null)
                return Current = AppUpdateState.Unknown(Channel, _currentVersion);

            if (!manifest.IsValid(out var validationError))
                return Current = new AppUpdateState(AppUpdateStatus.Error, Channel, _currentVersion, Error: validationError);
            if (!string.Equals(manifest.ReleaseChannel, expectedChannel, StringComparison.OrdinalIgnoreCase))
                return Current = new AppUpdateState(AppUpdateStatus.Error, Channel, _currentVersion, Error: "Update manifest channel is not supported by this build.");

            var updateAvailable = IsNewer(manifest.Version, _currentVersion);
            return Current = new AppUpdateState(
                updateAvailable ? AppUpdateStatus.UpdateAvailable : AppUpdateStatus.UpToDate,
                Channel,
                _currentVersion,
                manifest.Version,
                manifest.ReleaseNotesUri,
                updateAvailable ? manifest.PackageUri : null,
                updateAvailable ? AppUpdateActionKind.DownloadAndInstall : AppUpdateActionKind.None,
                ExpectedSha256: updateAvailable ? manifest.Sha256 : null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Current = new AppUpdateState(AppUpdateStatus.Error, Channel, _currentVersion, Error: exception.Message);
        }
    }

    private static bool IsNewer(string candidate, string current) =>
        SemanticVersion.TryParse(candidate, out var candidateVersion)
        && SemanticVersion.TryParse(current, out var currentVersion)
        && candidateVersion.CompareTo(currentVersion) > 0;

    private string ResolveExpectedChannel()
    {
        if (!string.IsNullOrWhiteSpace(_options.ExpectedReleaseChannel))
            return _options.ExpectedReleaseChannel;

        return SemanticVersion.TryParse(_currentVersion, out var current)
            && current.PreRelease is not null
            && current.PreRelease.StartsWith("alpha", StringComparison.OrdinalIgnoreCase)
            ? "alpha"
            : "stable";
    }
}
