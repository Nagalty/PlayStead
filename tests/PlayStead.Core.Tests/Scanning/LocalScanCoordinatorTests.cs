using PlayStead.Core.Library;
using PlayStead.Core.Scanning;

namespace PlayStead.Core.Tests.Scanning;

public sealed class LocalScanCoordinatorTests
{
    [Fact]
    public async Task ScanAll_keeps_successful_provider_results_when_another_provider_throws()
    {
        var observed = new DateTimeOffset(
            2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

        var steam = new StubSource(
            ProviderKind.Steam,
            SourceScanResult.Success(
                ProviderKind.Steam,
                observed,
                [
                    DiscoveredInstallation.Create(
                        ProviderKind.Steam,
                        "730",
                        "Counter-Strike 2",
                        @"G:\CS2",
                        10,
                        observed)
                ]));

        var broken = new ThrowingSource(
            ProviderKind.Epic,
            new IOException("boom"));

        var results = await new LocalScanCoordinator([steam, broken])
            .ScanAllAsync(CancellationToken.None);

        var steamResult = Assert.Single(
            results,
            x => x.Provider == ProviderKind.Steam);

        var epicResult = Assert.Single(
            results,
            x => x.Provider == ProviderKind.Epic);

        Assert.True(steamResult.IsComplete);
        Assert.Single(steamResult.Installations);

        Assert.False(epicResult.IsComplete);
        Assert.Empty(epicResult.Installations);
        Assert.Equal(nameof(IOException), epicResult.ErrorCode);
        Assert.Equal("boom", epicResult.ErrorMessage);
    }

    [Fact]
    public async Task ScanAll_rethrows_cancellation_requested_by_caller()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var source = new ThrowingSource(
            ProviderKind.Steam,
            new OperationCanceledException(cancellation.Token));

        var sut = new LocalScanCoordinator([source]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.ScanAllAsync(cancellation.Token));
    }

    private sealed class StubSource(
        ProviderKind provider,
        SourceScanResult result) : ILocalLibrarySource
    {
        public ProviderKind Provider { get; } = provider;

        public Task<SourceScanResult> ScanAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingSource(
        ProviderKind provider,
        Exception exception) : ILocalLibrarySource
    {
        public ProviderKind Provider { get; } = provider;

        public Task<SourceScanResult> ScanAsync(
            CancellationToken cancellationToken) =>
            Task.FromException<SourceScanResult>(exception);
    }
}
