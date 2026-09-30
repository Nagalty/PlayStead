using PlayStead.Core.LocalArtifacts;

namespace PlayStead.Core.Tests.LocalArtifacts;

public sealed class LocalArtifactComparisonServiceTests
{
    [Fact]
    public async Task Changed_text_returns_counts_and_redacts_sensitive_values()
    {
        using var fixture = new TempFiles("Mode=1\nApiToken=old\n", "Mode=0\nApiToken=new\n");
        var result = await new LocalArtifactComparisonService().CompareAsync(fixture.Reference, fixture.Current, CancellationToken.None);
        Assert.Equal(LocalArtifactComparisonStatus.Available, result.Status);
        Assert.Equal(2, result.AddedLineCount);
        Assert.Equal(2, result.RemovedLineCount);
        Assert.Contains(result.DiffHunks.SelectMany(x => x.Lines), x => x.Text == "ApiToken=••••••••");
    }

    [Fact]
    public async Task Identical_binary_and_oversized_files_are_rejected_without_writing()
    {
        using var identical = new TempFiles("same\n", "same\n");
        var service = new LocalArtifactComparisonService();
        Assert.Equal(LocalArtifactComparisonStatus.Identical, (await service.CompareAsync(identical.Reference, identical.Current, CancellationToken.None)).Status);
        var binary = new TempFiles("\0\u0001", "\0\u0002", ".bin");
        Assert.Equal(LocalArtifactComparisonStatus.UnsupportedFormat, (await service.CompareAsync(binary.Reference, binary.Current, CancellationToken.None)).Status);
        var missing = await service.CompareAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ini"), identical.Current, CancellationToken.None);
        Assert.Equal(LocalArtifactComparisonStatus.ReferenceUnavailable, missing.Status);
    }

    private sealed class TempFiles : IDisposable
    {
        public string Reference { get; } = Path.Combine(Path.GetTempPath(), $"ps-ref-{Guid.NewGuid():N}");
        public string Current { get; } = Path.Combine(Path.GetTempPath(), $"ps-current-{Guid.NewGuid():N}");
        public TempFiles(string reference, string current, string extension = ".ini")
        {
            Reference += extension;
            Current += extension;
            File.WriteAllText(Reference, reference);
            File.WriteAllText(Current, current);
        }
        public void Dispose()
        {
            File.Delete(Reference);
            File.Delete(Current);
        }
    }
}
