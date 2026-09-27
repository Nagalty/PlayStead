using PlayStead.Updater;

if (!UpdaterArguments.TryParse(args, out var parsed, out _))
{
    var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlayStead", "Data", "Updates");
    UpdaterLog.Write(root, "InvalidArguments");
    return (int)UpdaterExitCode.InvalidArguments;
}

var updatesRoot = parsed!.UpdatesRoot;
if (string.IsNullOrWhiteSpace(updatesRoot))
{
    var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlayStead", "Data", "Updates");
    UpdaterLog.Write(root, "InvalidArguments Reason=UpdatesRootMissing");
    return (int)UpdaterExitCode.InvalidArguments;
}

var updaterRuntimeRoot = Path.GetDirectoryName(Environment.ProcessPath)
    ?? throw new InvalidOperationException("Updater process path is unavailable.");
var code = await new UpdatePackageApplier(updatesRoot, updaterRuntimeRoot: updaterRuntimeRoot).ApplyAsync(parsed!);
return (int)code;
