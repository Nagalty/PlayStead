using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace PlayStead.Platform.SingleInstance;

public enum SingleInstanceResult
{
    Primary,
    Forwarded
}

public sealed class SingleInstanceGate : IDisposable
{
    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private bool _disposed;

    public SingleInstanceGate(
        string mutexName,
        string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        _mutexName = mutexName;
        _pipeName = pipeName;
    }

    public async Task<SingleInstanceResult> TryAcquireAsync(
        AppInvocation invocation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(invocation);

        if (_mutex is not null)
        {
            throw new InvalidOperationException(
                "Single-instance acquisition has already been attempted.");
        }

        _mutex = new Mutex(
            initiallyOwned: false,
            _mutexName,
            out var createdNew);

        if (createdNew)
        {
            return SingleInstanceResult.Primary;
        }

        await ForwardInvocationAsync(
            invocation,
            cancellationToken);

        return SingleInstanceResult.Forwarded;
    }

    private async Task ForwardInvocationAsync(
        AppInvocation invocation,
        CancellationToken cancellationToken)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await client.ConnectAsync(
            timeout: 5000,
            cancellationToken);

        using var writer = new StreamWriter(
            client,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true)
        {
            AutoFlush = true
        };

        using var reader = new StreamReader(
            client,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        var json = JsonSerializer.Serialize(invocation);

        await writer.WriteLineAsync(
            json.AsMemory(),
            cancellationToken);

        var acknowledgement = await reader.ReadLineAsync(
            cancellationToken);

        if (!string.Equals(
                acknowledgement,
                NamedPipeInvocationServer.Acknowledgement,
                StringComparison.Ordinal))
        {
            throw new IOException(
                "Primary PlayStead instance did not acknowledge the invocation.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _mutex?.Dispose();
        _mutex = null;
    }
}
