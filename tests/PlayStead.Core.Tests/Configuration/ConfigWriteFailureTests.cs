using PlayStead.Core.Configuration;
namespace PlayStead.Core.Tests.Configuration;
public sealed class ConfigWriteFailureTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PlayStead-write-" + Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("backup")]
    [InlineData("corrupt-backup")]
    [InlineData("write")]
    [InlineData("cancel")]
    public async Task Failure_preserves_current_and_retains_completed_backup(string failure)
    {
        Directory.CreateDirectory(root);
        var current = Path.Combine(root, "current.ini"); var reference = Path.Combine(root, "reference.ini");
        const string content = "[G]\nx=2\nnew=3\n";
        await File.WriteAllTextAsync(current, content); await File.WriteAllTextAsync(reference, "[G]\nx=1\n");
        var writer = new FailingWriter(failure);
        var service = new ConfigSemanticService(new ConfigDocumentAdapterResolver([new IniConfigDocumentAdapter()]), new Backup(failure), writer);
        var changes = await service.CompareConfigAsync(current, reference, CancellationToken.None);
        if (failure == "cancel")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RestoreConfigValuesAsync(current, reference, [changes.Single(x => x.CanRestore).Path], CancellationToken.None));
        else
        {
            var result = await service.RestoreConfigValuesAsync(current, reference, [changes.Single(x => x.CanRestore).Path], CancellationToken.None);
            Assert.False(result.Succeeded);
            if (failure == "write") Assert.True(File.Exists(result.BackupPath));
        }
        Assert.Equal(content, await File.ReadAllTextAsync(current));
        Assert.Equal(failure is "write" or "cancel", writer.Called);
    }
    [Fact]
    public async Task Atomic_writer_cancellation_leaves_original_and_no_temporary_file()
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "current.ini"); await File.WriteAllTextAsync(path, "original");
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AtomicConfigFileWriter().WriteAsync(path, "replacement", cts.Token));
        Assert.Equal("original", await File.ReadAllTextAsync(path)); Assert.Single(Directory.GetFiles(root));
    }
    private sealed class Backup(string failure) : IConfigBackupWriter
    {
        public async Task<string> CreateBackupAsync(string path, CancellationToken token)
        {
            if (failure == "backup") throw new IOException("backup denied");
            var backup = path + ".bak";
            await File.WriteAllTextAsync(backup, failure == "corrupt-backup" ? "bad" : await File.ReadAllTextAsync(path, token), token);
            return backup;
        }
    }
    private sealed class FailingWriter(string failure) : IConfigAtomicWriter
    {
        public bool Called;
        public Task WriteAsync(string path, string content, CancellationToken token)
        {
            Called = true;
            if (failure == "cancel") throw new OperationCanceledException();
            throw new IOException("atomic replacement denied");
        }
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
