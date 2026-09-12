using PlayStead.Providers.Steam.Remote;

namespace PlayStead.Providers.Tests.Steam.Remote;

public sealed class SystemSteamCmdProcessInvokerTests
{
    [Fact]
    public async Task InvokeAsync_captures_stdout_stderr_and_exit_code()
    {
        var commandProcessor =
            Environment.GetEnvironmentVariable("COMSPEC");

        Assert.False(
            string.IsNullOrWhiteSpace(commandProcessor));

        var sut = new SystemSteamCmdProcessInvoker();

        var result = await sut.InvokeAsync(
            new SteamCmdProcessRequest(
                commandProcessor!,
                [
                    "/d",
                    "/c",
                    "echo OUT & echo ERR 1>&2 & exit /b 7"
                ]),
            CancellationToken.None);

        Assert.Equal(7, result.ExitCode);
        Assert.Contains(
            "OUT",
            result.StandardOutput,
            StringComparison.Ordinal);

        Assert.Contains(
            "ERR",
            result.StandardError,
            StringComparison.Ordinal);

        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task InvokeAsync_honors_cancellation_for_a_running_process()
    {
        var commandProcessor =
            Environment.GetEnvironmentVariable("COMSPEC");

        Assert.False(
            string.IsNullOrWhiteSpace(commandProcessor));

        var sut = new SystemSteamCmdProcessInvoker();

        using var cts = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.InvokeAsync(
                new SteamCmdProcessRequest(
                    commandProcessor!,
                    [
                        "/d",
                        "/c",
                        "ping 127.0.0.1 -n 30 >nul"
                    ]),
                cts.Token));
    }
}
