using System.IO.Compression;
using PlayStead.Updater;

namespace PlayStead.Updater.Tests;

public sealed class UpdatePackageApplierTests
{
    [Fact]
    public async Task Rejects_missing_package_and_package_outside_updates_root()
    {
        var root = CreateRoot(out var updates, out var target, out var exe);
        var applier = new UpdatePackageApplier(updates, updaterRuntimeRoot: updates);
        var missing = await applier.ApplyAsync(new UpdaterArguments(0, Path.Combine(updates, "missing.zip"), target, exe, "1.2.3"));
        Assert.Equal(UpdaterExitCode.ValidationFailed, missing);
        var outside = Path.Combine(root, "outside.zip");
        await File.WriteAllBytesAsync(outside, []);
        var rejected = await applier.ApplyAsync(new UpdaterArguments(0, outside, target, exe, "1.2.3"));
        Assert.Equal(UpdaterExitCode.ValidationFailed, rejected);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Rejects_package_in_non_canonical_root_even_when_legacy_root_exists()
    {
        var root = CreateRoot(out var updates, out var target, out var exe);
        var canonicalRoot = Path.Combine(root, "Data", "Updates");
        Directory.CreateDirectory(canonicalRoot);
        var legacyRoot = Path.Combine(root, "Updates", "1.2.3");
        Directory.CreateDirectory(legacyRoot);
        var package = await CreatePackage(legacyRoot, ("PlayStead.UI.exe", "new"));

        var code = await new UpdatePackageApplier(canonicalRoot, updaterRuntimeRoot: canonicalRoot)
            .ApplyAsync(new UpdaterArguments(0, package, target, exe, "1.2.3", canonicalRoot));

        Assert.Equal(UpdaterExitCode.ValidationFailed, code);
        Assert.Equal("old", await File.ReadAllTextAsync(exe));
        Directory.Delete(root, true);
    }

    [Fact]
    public void Requires_an_explicit_updates_root_argument()
    {
        var valid = new[]
        {
            "--pid", "0", "--package", "package.zip", "--target-dir", "install",
            "--exe", "install\\PlayStead.UI.exe", "--version", "1.2.3", "--updates-root", "updates"
        };
        Assert.True(UpdaterArguments.TryParse(valid, out var parsed, out _));
        Assert.Equal("updates", parsed!.UpdatesRoot);

        var missing = valid[..^2];
        Assert.False(UpdaterArguments.TryParse(missing, out _, out _));
    }

    [Fact]
    public async Task Rejects_runner_inside_target_or_outside_updates_root()
    {
        var root = CreateRoot(out var updates, out var target, out var exe);
        var updatesRoot = Path.GetDirectoryName(updates)!;
        var package = await CreatePackage(updates, ("PlayStead.UI.exe", "new"));

        var insideTarget = await new UpdatePackageApplier(updatesRoot, updaterRuntimeRoot: target)
            .ApplyAsync(new UpdaterArguments(0, package, target, exe, "1.2.3", updatesRoot));
        Assert.Equal(UpdaterExitCode.ValidationFailed, insideTarget);

        var outsideRoot = Path.Combine(root, "OutsideRunner");
        Directory.CreateDirectory(outsideRoot);
        var outside = await new UpdatePackageApplier(updatesRoot, updaterRuntimeRoot: outsideRoot)
            .ApplyAsync(new UpdaterArguments(0, package, target, exe, "1.2.3", updatesRoot));
        Assert.Equal(UpdaterExitCode.ValidationFailed, outside);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Missing_package_logs_the_first_validation_reason()
    {
        var root = CreateRoot(out var updates, out var target, out var exe);
        var code = await new UpdatePackageApplier(updates, updaterRuntimeRoot: updates)
            .ApplyAsync(new UpdaterArguments(0, Path.Combine(updates, "missing.zip"), target, exe, "1.2.3"));

        Assert.Equal(UpdaterExitCode.ValidationFailed, code);
        var log = await File.ReadAllTextAsync(Path.Combine(updates, "updater.log"));
        Assert.Contains("ValidationFailed Reason=PackageMissing", log, StringComparison.Ordinal);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Invalid_target_and_corrupt_zip_are_rejected()
    {
        var root = CreateRoot(out var updates, out var target, out var exe);
        var package = Path.Combine(updates, "package.zip");
        await File.WriteAllTextAsync(package, "not zip");
        var invalidTarget = await new UpdatePackageApplier(updates, updaterRuntimeRoot: updates)
            .ApplyAsync(new UpdaterArguments(0, package, Path.Combine(root, "missing"), exe, "1.2.3"));
        Assert.Equal(UpdaterExitCode.ValidationFailed, invalidTarget);
        var corrupt = await new UpdatePackageApplier(updates, updaterRuntimeRoot: updates)
            .ApplyAsync(new UpdaterArguments(0, package, target, exe, "1.2.3"));
        Assert.Equal(UpdaterExitCode.ApplyFailed, corrupt);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Waits_for_active_process_and_continues_when_it_is_closed()
    {
        var root = CreateRoot(out var updates, out var target, out var exe);
        var package = await CreatePackage(updates, ("PlayStead.UI.exe", "new"));
        var controller = new FakeProcessController();
        var code = await new UpdatePackageApplier(updates, controller, updaterRuntimeRoot: updates)
            .ApplyAsync(new UpdaterArguments(42, package, target, exe, "1.2.3"));
        Assert.Equal(UpdaterExitCode.Success, code);
        Assert.Equal(42, controller.WaitedProcessId);
        Directory.Delete(root, true);
    }
    [Fact]
    public async Task Applies_package_and_relaunches_without_touching_user_data()
    {
        var root = Path.Combine(Path.GetTempPath(), "playstead-updater-" + Guid.NewGuid().ToString("N"));
        var updates = Path.Combine(root, "Updates", "1.2.3");
        var target = Path.Combine(root, "App");
        Directory.CreateDirectory(updates); Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "PlayStead.UI.exe"), "old");
        await File.WriteAllTextAsync(Path.Combine(root, "user.db"), "keep");
        var package = Path.Combine(updates, "package.zip");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("PlayStead.UI.exe");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("new");
        }
        var controller = new FakeProcessController();
        var code = await new UpdatePackageApplier(Path.Combine(root, "Updates"), controller, updaterRuntimeRoot: Path.Combine(root, "Updates"))
            .ApplyAsync(new UpdaterArguments(0, package, target, Path.Combine(target, "PlayStead.UI.exe"), "1.2.3"));
        Assert.Equal(UpdaterExitCode.Success, code);
        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(target, "PlayStead.UI.exe")));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(root, "user.db")));
        Assert.True(controller.Started);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Rejects_zip_slip_without_writing_outside_target()
    {
        var root = Path.Combine(Path.GetTempPath(), "playstead-updater-" + Guid.NewGuid().ToString("N"));
        var updates = Path.Combine(root, "Updates", "1.2.3");
        var target = Path.Combine(root, "App");
        Directory.CreateDirectory(updates); Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "PlayStead.UI.exe"), "old");
        var package = Path.Combine(updates, "package.zip");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        { await using var writer = new StreamWriter(archive.CreateEntry("..\\outside.txt").Open()); await writer.WriteAsync("bad"); }
        var code = await new UpdatePackageApplier(Path.Combine(root, "Updates"), new FakeProcessController(), updaterRuntimeRoot: Path.Combine(root, "Updates"))
            .ApplyAsync(new UpdaterArguments(0, package, target, Path.Combine(target, "PlayStead.UI.exe"), "1.2.3"));
        Assert.Equal(UpdaterExitCode.ApplyFailed, code);
        Assert.False(File.Exists(Path.Combine(root, "outside.txt")));
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Relaunch_failure_restores_previous_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "playstead-updater-" + Guid.NewGuid().ToString("N"));
        var updates = Path.Combine(root, "Updates", "1.2.3");
        var target = Path.Combine(root, "App");
        Directory.CreateDirectory(updates); Directory.CreateDirectory(target);
        var exe = Path.Combine(target, "PlayStead.UI.exe");
        await File.WriteAllTextAsync(exe, "old");
        var package = Path.Combine(updates, "package.zip");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        { await using var writer = new StreamWriter(archive.CreateEntry("PlayStead.UI.exe").Open()); await writer.WriteAsync("new"); }
        var code = await new UpdatePackageApplier(Path.Combine(root, "Updates"), new FakeProcessController { StartResult = false }, updaterRuntimeRoot: Path.Combine(root, "Updates"))
            .ApplyAsync(new UpdaterArguments(0, package, target, exe, "1.2.3"));
        Assert.Equal(UpdaterExitCode.RelaunchFailed, code);
        Assert.Equal("old", await File.ReadAllTextAsync(exe));
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Mid_copy_failure_rolls_back_old_files_and_removes_new_files()
    {
        var root = CreateRoot(out var updates, out var target, out var exe);
        await File.WriteAllTextAsync(Path.Combine(target, "A.dll"), "v1-A");
        await File.WriteAllTextAsync(Path.Combine(target, "B.dll"), "v1-B");
        var package = await CreatePackage(updates, ("A.dll", "v2-A"), ("C.dll", "v2-C"));
        var code = await new UpdatePackageApplier(updates, new FakeProcessController(), updaterRuntimeRoot: updates,
            copyFile: (source, destination) =>
            {
                if (Path.GetFileName(destination).Equals("C.dll", StringComparison.OrdinalIgnoreCase)) throw new IOException("injected copy failure");
                File.Copy(source, destination, true);
            }).ApplyAsync(new UpdaterArguments(0, package, target, exe, "1.2.3"));
        Assert.Equal(UpdaterExitCode.ApplyFailed, code);
        Assert.Equal("v1-A", await File.ReadAllTextAsync(Path.Combine(target, "A.dll")));
        Assert.Equal("v1-B", await File.ReadAllTextAsync(Path.Combine(target, "B.dll")));
        Assert.False(File.Exists(Path.Combine(target, "C.dll")));
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Files_absent_from_overlay_package_are_preserved()
    {
        var root = CreateRoot(out var updates, out var target, out var exe);
        await File.WriteAllTextAsync(Path.Combine(target, "B.dll"), "v1-B");
        var package = await CreatePackage(updates, ("PlayStead.UI.exe", "new"));
        var code = await new UpdatePackageApplier(updates, new FakeProcessController(), updaterRuntimeRoot: updates)
            .ApplyAsync(new UpdaterArguments(0, package, target, exe, "1.2.3"));
        Assert.Equal(UpdaterExitCode.Success, code);
        Assert.Equal("v1-B", await File.ReadAllTextAsync(Path.Combine(target, "B.dll")));
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Detached_runner_can_replace_runtime_file_in_installation()
    {
        var root = CreateRoot(out var updates, out var target, out var exe);
        var runner = Path.Combine(updates, "updater-runner");
        Directory.CreateDirectory(runner);
        await File.WriteAllTextAsync(Path.Combine(target, "clrjit.dll"), "old-runtime");
        await File.WriteAllTextAsync(Path.Combine(runner, "PlayStead.Updater.exe"), "runner");
        var package = await CreatePackage(updates, ("clrjit.dll", "new-runtime"));

        var code = await new UpdatePackageApplier(updates, new FakeProcessController(), updaterRuntimeRoot: updates)
            .ApplyAsync(new UpdaterArguments(0, package, target, exe, "1.2.3", Path.GetDirectoryName(updates)));

        Assert.Equal(UpdaterExitCode.Success, code);
        Assert.Equal("new-runtime", await File.ReadAllTextAsync(Path.Combine(target, "clrjit.dll")));
        Assert.True(File.Exists(Path.Combine(runner, "PlayStead.Updater.exe")));
        Assert.DoesNotContain(target, runner, StringComparison.OrdinalIgnoreCase);
        Directory.Delete(root, true);
    }

    private static string CreateRoot(out string updates, out string target, out string exe)
    {
        var root = Path.Combine(Path.GetTempPath(), "playstead-updater-" + Guid.NewGuid().ToString("N"));
        updates = Path.Combine(root, "Updates", "1.2.3"); target = Path.Combine(root, "App");
        Directory.CreateDirectory(updates); Directory.CreateDirectory(target);
        exe = Path.Combine(target, "PlayStead.UI.exe"); File.WriteAllText(exe, "old");
        return root;
    }

    private static async Task<string> CreatePackage(string updates, params (string Name, string Content)[] files)
    {
        var package = Path.Combine(updates, "package.zip");
        using var archive = ZipFile.Open(package, ZipArchiveMode.Create);
        foreach (var file in files)
        {
            await using var writer = new StreamWriter(archive.CreateEntry(file.Name).Open());
            await writer.WriteAsync(file.Content);
        }
        return package;
    }

    private sealed class FakeProcessController : IUpdateProcessController
    {
        public bool Started { get; private set; }
        public bool StartResult { get; init; } = true;
        public int WaitedProcessId { get; private set; }
        public Task WaitForExitAsync(int processId, TimeSpan timeout, CancellationToken cancellationToken) { WaitedProcessId = processId; return Task.CompletedTask; }
        public bool Start(string executablePath, string arguments) { Started = true; return StartResult; }
    }
}
