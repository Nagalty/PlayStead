using System.Net;
using System.Security.Cryptography;
using System.Text;
using PlayStead.Core.Updates;
using PlayStead.Providers.Updates;

namespace PlayStead.Providers.Tests.Updates;

public sealed class GitHubAppUpdatePackageDownloaderTests
{
    [Fact]
    public async Task Valid_package_is_streamed_hashed_and_moved_to_final_path()
    {
        var bytes = Encoding.UTF8.GetBytes("package");
        var root = CreateRoot();
        try
        {
            using var client = new HttpClient(new ResponseHandler(bytes));
            var downloader = new GitHubAppUpdatePackageDownloader(client, root);
            var progressValues = new List<AppUpdateDownloadProgress>();

            var result = await downloader.DownloadAsync(
                new Uri("https://example.invalid/package.zip"),
                Convert.ToHexString(SHA256.HashData(bytes)),
                "0.4.3-alpha2",
                new Progress<AppUpdateDownloadProgress>(progressValues.Add));

            Assert.True(File.Exists(result.LocalPackagePath));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(result.LocalPackagePath));
            Assert.NotEmpty(progressValues);
            Assert.False(File.Exists(Path.Combine(root, "0.4.3-alpha2", "package.tmp")));
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task Hash_mismatch_removes_partial_package_and_fails()
    {
        var root = CreateRoot();
        try
        {
            using var client = new HttpClient(new ResponseHandler(Encoding.UTF8.GetBytes("package")));
            var downloader = new GitHubAppUpdatePackageDownloader(client, root);

            await Assert.ThrowsAsync<InvalidDataException>(() => downloader.DownloadAsync(
                new Uri("https://example.invalid/package.zip"),
                new string('0', 64),
                "0.4.3"));

            Assert.False(File.Exists(Path.Combine(root, "0.4.3", "package.tmp")));
            Assert.False(File.Exists(Path.Combine(root, "0.4.3", "package.zip")));
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task Non_https_package_uri_is_rejected_before_request()
    {
        var root = CreateRoot();
        try
        {
            using var client = new HttpClient(new ResponseHandler([]));
            var downloader = new GitHubAppUpdatePackageDownloader(client, root);

            await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadAsync(
                new Uri("http://example.invalid/package.zip"),
                new string('0', 64),
                "0.4.3"));
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task Cancellation_is_honored_before_request()
    {
        var root = CreateRoot();
        try
        {
            using var client = new HttpClient(new ResponseHandler([]));
            var downloader = new GitHubAppUpdatePackageDownloader(client, root);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAsync(
                new Uri("https://example.invalid/package.zip"),
                new string('0', 64),
                "0.4.3",
                cancellationToken: cancellation.Token));
        }
        finally { TryDelete(root); }
    }

    private static string CreateRoot() => Path.Combine(Path.GetTempPath(), "PlayStead-UpdateTests", Guid.NewGuid().ToString("N"));
    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    private sealed class ResponseHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes),
                RequestMessage = new HttpRequestMessage(request.Method, request.RequestUri)
            });
    }
}
