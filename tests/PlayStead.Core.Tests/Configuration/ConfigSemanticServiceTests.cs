using PlayStead.Core.Configuration;

namespace PlayStead.Core.Tests.Configuration;

public sealed class ConfigSemanticServiceTests
{
    [Theory]
    [InlineData("ini", "[G]\nx=2\n", "[G]\nx=1\n", "[G]\nx=3\n")]
    [InlineData("json", "{\"x\":2}", "{\"x\":1}", "{\"x\":3}")]
    public async Task Changed_since_comparison_is_rejected_without_write(string extension, string text, string old, string changed)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var current = Path.Combine(root, "current." + extension); var reference = Path.Combine(root, "reference." + extension);
        try
        {
            await File.WriteAllTextAsync(current, text); await File.WriteAllTextAsync(reference, old);
            var service = new ConfigSemanticService(new ConfigDocumentAdapterResolver([new IniConfigDocumentAdapter(), new JsonConfigDocumentAdapter()]), new FileConfigBackupWriter(), new AtomicConfigFileWriter());
            var changes = await service.CompareConfigAsync(current, reference, CancellationToken.None);
            await File.WriteAllTextAsync(current, changed);
            var result = await service.RestoreConfigValuesAsync(current, reference, [changes[0].Path], CancellationToken.None);
            Assert.False(result.Succeeded); Assert.Null(result.BackupPath);
            Assert.Equal(changed, await File.ReadAllTextAsync(current));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Selective_restore_keeps_unselected_current_values_and_creates_backup()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var current = Path.Combine(root, "settings.ini"); var reference = Path.Combine(root, "reference.ini");
        await File.WriteAllTextAsync(current, "[Graphics]\nVsync=false\nNewKey=1\n");
        await File.WriteAllTextAsync(reference, "[Graphics]\nVsync=true\nWidth=1920\n");
        var service = new ConfigSemanticService(new ConfigDocumentAdapterResolver([new IniConfigDocumentAdapter(), new JsonConfigDocumentAdapter()]), new FileConfigBackupWriter(), new AtomicConfigFileWriter());
        var changes = await service.CompareConfigAsync(current, reference, CancellationToken.None);
        var vsync = Assert.Single(changes, x => x.Path.EndsWith("Vsync", StringComparison.OrdinalIgnoreCase));
        var result = await service.RestoreConfigValuesAsync(current, reference, [vsync.Path], CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.BackupPath);
        var text = await File.ReadAllTextAsync(current);
        Assert.Contains("Vsync=true", text);
        Assert.Contains("NewKey=1", text);
    }

    [Fact]
    public async Task Unsupported_format_is_read_only()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var current = Path.Combine(root, "settings.toml"); var reference = Path.Combine(root, "reference.toml");
        await File.WriteAllTextAsync(current, "value=2"); await File.WriteAllTextAsync(reference, "value=1");
        var service = new ConfigSemanticService(new ConfigDocumentAdapterResolver([new IniConfigDocumentAdapter(), new JsonConfigDocumentAdapter()]), new FileConfigBackupWriter(), new AtomicConfigFileWriter());
        var result = await service.RestoreConfigValuesAsync(current, reference, ["value"], CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Null(result.BackupPath);
        Assert.Equal("value=2", await File.ReadAllTextAsync(current));
    }
}
