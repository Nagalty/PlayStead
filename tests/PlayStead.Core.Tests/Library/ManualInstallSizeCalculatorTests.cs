using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Library;

public sealed class ManualInstallSizeCalculatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-ManualSize-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Size_is_calculated_from_install_root_not_working_directory()
    {
        var workingDirectory = Directory.CreateDirectory(Path.Combine(_root, "Retail")).FullName;
        var root = Directory.CreateDirectory(Path.Combine(_root, "Game")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(root, "content.bin"), new byte[7]);
        await File.WriteAllBytesAsync(Path.Combine(workingDirectory, "game.exe"), new byte[3]);

        var size = await ManualInstallSizeCalculator.TryCalculateAsync(root, CancellationToken.None);

        Assert.Equal(7, size);
    }

    [Fact]
    public async Task Missing_or_invalid_root_returns_unknown()
    {
        var size = await ManualInstallSizeCalculator.TryCalculateAsync(
            Path.Combine(_root, "missing"), CancellationToken.None);

        Assert.Null(size);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
