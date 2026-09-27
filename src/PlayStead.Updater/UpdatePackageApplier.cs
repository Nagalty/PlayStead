using System.IO.Compression;

namespace PlayStead.Updater;

public enum UpdaterExitCode { Success = 0, InvalidArguments = 2, ValidationFailed = 3, ApplyFailed = 4, RelaunchFailed = 5 }

public sealed class UpdatePackageApplier
{
    private readonly IUpdateProcessController _process;
    private readonly string _updatesRoot;
    private readonly string _updaterRuntimeRoot;
    private readonly TimeSpan _waitTimeout;
    private readonly Action<string, string>? _copyFile;

    public UpdatePackageApplier(string updatesRoot, IUpdateProcessController? process = null, TimeSpan? waitTimeout = null, string? updaterRuntimeRoot = null, Action<string, string>? copyFile = null)
    {
        _updatesRoot = Path.GetFullPath(updatesRoot);
        _updaterRuntimeRoot = Path.GetFullPath(updaterRuntimeRoot ?? Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory);
        _process = process ?? new SystemUpdateProcessController();
        _waitTimeout = waitTimeout ?? TimeSpan.FromMinutes(2);
        _copyFile = copyFile;
    }

    public async Task<UpdaterExitCode> ApplyAsync(UpdaterArguments arguments, CancellationToken cancellationToken = default)
    {
        var updatesRoot = Path.GetFullPath(arguments.UpdatesRoot ?? _updatesRoot);
        var updaterExecutablePath = Path.Combine(_updaterRuntimeRoot, "PlayStead.Updater.exe");
        UpdaterLog.Write(updatesRoot, $"ApplyStart Version={arguments.Version} UpdaterExecutablePath=\"{updaterExecutablePath}\"");
        var package = Path.GetFullPath(arguments.PackagePath);
        var target = Path.GetFullPath(arguments.TargetDirectory);
        var executable = Path.GetFullPath(arguments.ExecutablePath);
        UpdaterLog.Write(updatesRoot, $"ValidationInput PackagePath=\"{package}\" PackageExists={File.Exists(package)} UpdatesRoot=\"{updatesRoot}\" UpdaterRuntimeRoot=\"{_updaterRuntimeRoot}\" UpdaterExecutablePath=\"{updaterExecutablePath}\" TargetDirectory=\"{target}\" RelaunchExecutable=\"{executable}\" ExecutableExists={File.Exists(executable)}");
        if (!IsUnder(updaterExecutablePath, updatesRoot))
        {
            UpdaterLog.Write(updatesRoot, "ValidationFailed Reason=UpdaterOutsideUpdatesRoot");
            return UpdaterExitCode.ValidationFailed;
        }
        if (IsUnder(updaterExecutablePath, target))
        {
            UpdaterLog.Write(updatesRoot, "ValidationFailed Reason=UpdaterInsideTarget");
            return UpdaterExitCode.ValidationFailed;
        }
        if (!IsUnder(package, updatesRoot))
        {
            UpdaterLog.Write(updatesRoot, "ValidationFailed Reason=PackageOutsideUpdatesRoot");
            return UpdaterExitCode.ValidationFailed;
        }
        if (!File.Exists(package))
        {
            UpdaterLog.Write(updatesRoot, "ValidationFailed Reason=PackageMissing");
            return UpdaterExitCode.ValidationFailed;
        }
        if (!Directory.Exists(target))
        {
            UpdaterLog.Write(updatesRoot, "ValidationFailed Reason=InstallDirectoryMissing");
            return UpdaterExitCode.ValidationFailed;
        }
        if (!IsUnder(executable, target))
        {
            UpdaterLog.Write(updatesRoot, "ValidationFailed Reason=RelaunchOutsideTarget");
            return UpdaterExitCode.ValidationFailed;
        }
        if (!File.Exists(executable))
        {
            UpdaterLog.Write(updatesRoot, "ValidationFailed Reason=RelaunchExecutableMissing");
            return UpdaterExitCode.ValidationFailed;
        }
        if (!package.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            UpdaterLog.Write(updatesRoot, "ValidationFailed PackageExtension");
            return UpdaterExitCode.ValidationFailed;
        }

        var packageDirectory = Path.GetDirectoryName(package)!;
        var staging = Path.Combine(packageDirectory, "staging");
        var backup = Path.Combine(packageDirectory, "backup");
        var applied = new List<string>();
        try
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            Directory.CreateDirectory(staging);
            ExtractSafely(package, staging);
            if (!Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).Any()) return UpdaterExitCode.ValidationFailed;
            await _process.WaitForExitAsync(arguments.ProcessId, _waitTimeout, cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(backup);
            foreach (var source in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(staging, source);
                var destination = Path.GetFullPath(Path.Combine(target, relative));
                if (!IsUnder(destination, target)) throw new InvalidDataException("Archive path escapes target directory.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (File.Exists(destination))
                {
                    var backupPath = Path.Combine(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                    File.Copy(destination, backupPath, true);
                }
                applied.Add(destination);
                if (_copyFile is null) File.Copy(source, destination, true);
                else _copyFile(source, destination);
            }
            var relaunched = _process.Start(executable, "--updated-from " + Quote(arguments.Version));
            if (!relaunched)
            {
                Rollback(applied, target, backup);
                return UpdaterExitCode.RelaunchFailed;
            }
            Directory.Delete(staging, true);
            Directory.Delete(backup, true);
            UpdaterLog.Write(updatesRoot, "ApplyCompleted");
            return UpdaterExitCode.Success;
        }
        catch (OperationCanceledException)
        {
            Rollback(applied, target, backup);
            UpdaterLog.Write(updatesRoot, "ApplyCanceled");
            return UpdaterExitCode.ApplyFailed;
        }
        catch (Exception)
        {
            Rollback(applied, target, backup);
            UpdaterLog.Write(updatesRoot, "ApplyFailed");
            return UpdaterExitCode.ApplyFailed;
        }
    }

    private static void ExtractSafely(string package, string staging)
    {
        using var archive = ZipFile.OpenRead(package);
        foreach (var entry in archive.Entries)
        {
            var destination = Path.GetFullPath(Path.Combine(staging, entry.FullName));
            if (!IsUnder(destination, staging)) throw new InvalidDataException("Archive contains a path traversal entry.");
            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, true);
        }
    }

    private static bool IsUnder(string path, string root)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static void Rollback(IEnumerable<string> applied, string target, string backup)
    {
        foreach (var destination in applied)
        {
            var relative = Path.GetRelativePath(target, destination);
            var backupPath = Path.Combine(backup, relative);
            if (File.Exists(backupPath)) File.Copy(backupPath, destination, true);
            else if (File.Exists(destination)) File.Delete(destination);
        }
    }
}
