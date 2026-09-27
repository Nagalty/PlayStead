using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using PlayStead.Core.Updates;

namespace PlayStead.Providers.Updates;

public sealed class GitHubAppUpdatePackageDownloader : IAppUpdatePackageDownloader
{
    private readonly HttpClient _httpClient;
    private readonly string _updatesRoot;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public GitHubAppUpdatePackageDownloader(HttpClient httpClient, string updatesRoot)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _updatesRoot = Path.GetFullPath(updatesRoot ?? throw new ArgumentNullException(nameof(updatesRoot)));
        CleanupAbandonedTemporaryFiles();
    }

    public async Task<AppUpdateDownloadResult> DownloadAsync(
        Uri packageUri,
        string expectedSha256,
        string version,
        IProgress<AppUpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (packageUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Update package URI must use HTTPS.");
        if (string.IsNullOrWhiteSpace(expectedSha256) || expectedSha256.Length != 64 || !expectedSha256.All(IsHex))
            throw new InvalidDataException("Update package SHA-256 is invalid.");
        if (!SemanticVersion.TryParse(version, out _))
            throw new InvalidDataException("Update package version is invalid.");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var directory = Path.Combine(_updatesRoot, version);
        var temporaryPath = Path.Combine(directory, "package.tmp");
        var packagePath = Path.Combine(directory, "package.zip");
        try
        {
            Directory.CreateDirectory(directory);
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            Trace.WriteLine($"[APP-UPDATE] DownloadStart Version={version}");

            using var response = await _httpClient.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var finalUri = response.RequestMessage?.RequestUri;
            if (finalUri is null || finalUri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Update package redirected to a non-HTTPS URI.");

            var total = response.Content.Headers.ContentLength;
            long received = 0;
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;
                    progress?.Report(new AppUpdateDownloadProgress(received, total));
                }
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var actualHash = await ComputeSha256Async(temporaryPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                Trace.WriteLine($"[APP-UPDATE] HashMismatch Version={version}");
                File.Delete(temporaryPath);
                throw new InvalidDataException("Downloaded update hash does not match the manifest.");
            }

            if (File.Exists(packagePath)) File.Delete(packagePath);
            File.Move(temporaryPath, packagePath);
            Trace.WriteLine($"[APP-UPDATE] HashVerified Version={version}");
            Trace.WriteLine($"[APP-UPDATE] DownloadCompleted Version={version} Bytes={received}");
            return new AppUpdateDownloadResult(packagePath, received, total);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool IsHex(char value) => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private void CleanupAbandonedTemporaryFiles()
    {
        if (!Directory.Exists(_updatesRoot)) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(_updatesRoot, "package.tmp", SearchOption.AllDirectories))
                TryDelete(file);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
