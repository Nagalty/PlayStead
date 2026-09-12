using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace PlayStead.Platform.SingleInstance;

public sealed class NamedPipeInvocationServer : IAsyncDisposable
{
    internal const string Acknowledgement = "OK";

    private readonly string _pipeName;
    private CancellationTokenSource? _lifetime;
    private Task? _serverLoop;
    private bool _disposed;

    public NamedPipeInvocationServer(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        _pipeName = pipeName;
    }

    public event EventHandler<AppInvocation>? InvocationReceived;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_serverLoop is not null)
        {
            throw new InvalidOperationException(
                "Named pipe invocation server has already been started.");
        }

        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

        _serverLoop = RunAsync(_lifetime.Token);

        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

                await HandleClientAsync(
                        server,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
                // A client may disconnect before the exchange completes.
                // Keep the primary server alive for the next invocation.
            }
        }
    }

    private async Task HandleClientAsync(
        NamedPipeServerStream server,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            server,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        using var writer = new StreamWriter(
            server,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true)
        {
            AutoFlush = true
        };

        var line = await reader.ReadLineAsync(
                cancellationToken)
            .ConfigureAwait(false);

        if (line is null)
        {
            return;
        }

        try
        {
            var invocation =
                JsonSerializer.Deserialize<AppInvocation>(line);

            if (invocation is not null)
            {
                InvocationReceived?.Invoke(
                    this,
                    invocation);
            }
        }
        catch (JsonException)
        {
            // Invalid invocation payloads are intentionally ignored.
        }

        await writer.WriteLineAsync(
                Acknowledgement.AsMemory(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_lifetime is not null)
        {
            await _lifetime.CancelAsync()
                .ConfigureAwait(false);
        }

        if (_serverLoop is not null)
        {
            try
            {
                await _serverLoop
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
        }

        _lifetime?.Dispose();
        _lifetime = null;
        _serverLoop = null;
    }
}
