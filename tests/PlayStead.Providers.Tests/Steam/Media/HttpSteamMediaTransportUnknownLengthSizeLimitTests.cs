using System.Net;
using System.Net.Http.Headers;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class HttpSteamMediaTransportUnknownLengthSizeLimitTests
{
    private const long MaxMediaBytes = 25L * 1024 * 1024;

    [Fact]
    public async Task TryDownloadAsync_stops_after_limit_when_ContentLength_is_unknown()
    {
        await using var source = new CountingStream(
            MaxMediaBytes + (64 * 1024));

        using var content = new StreamContent(source);
        content.Headers.ContentType =
            new MediaTypeHeaderValue("image/jpeg");

        var handler = new SuccessHandler(content);
        using var httpClient = new HttpClient(handler);
        var transport = new HttpSteamMediaTransport(httpClient);

        var payload = await transport.TryDownloadAsync(
            "1874880",
            GameMediaAssetType.Cover,
            [new Uri("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg")],
            CancellationToken.None);

        Assert.Null(payload);
        Assert.True(
            source.BytesRead <= MaxMediaBytes + 1,
            $"Transport read {source.BytesRead} bytes; expected at most {MaxMediaBytes + 1}.");
    }

    private sealed class SuccessHandler : HttpMessageHandler
    {
        private readonly HttpContent _content;

        public SuccessHandler(HttpContent content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = _content,
                    RequestMessage = request
                });
        }
    }

    private sealed class CountingStream : Stream
    {
        private readonly long _totalBytes;
        private long _position;

        public CountingStream(long totalBytes)
        {
            _totalBytes = totalBytes;
        }

        public long BytesRead => _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            var remaining = _totalBytes - _position;
            if (remaining <= 0)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(
                count,
                remaining);

            Array.Clear(
                buffer,
                offset,
                bytesToRead);

            _position += bytesToRead;
            return bytesToRead;
        }

        public override int Read(Span<byte> buffer)
        {
            var remaining = _totalBytes - _position;
            if (remaining <= 0)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(
                buffer.Length,
                remaining);

            buffer[..bytesToRead].Clear();
            _position += bytesToRead;
            return bytesToRead;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = _totalBytes - _position;
            if (remaining <= 0)
            {
                return ValueTask.FromResult(0);
            }

            var bytesToRead = (int)Math.Min(
                buffer.Length,
                remaining);

            buffer.Span[..bytesToRead].Clear();
            _position += bytesToRead;

            return ValueTask.FromResult(bytesToRead);
        }

        public override void Flush()
        {
        }

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();
    }
}
