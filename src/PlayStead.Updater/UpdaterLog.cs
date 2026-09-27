namespace PlayStead.Updater;

internal static class UpdaterLog
{
    public static void Write(string updatesRoot, string message)
    {
        try
        {
            Directory.CreateDirectory(updatesRoot);
            File.AppendAllText(Path.Combine(updatesRoot, "updater.log"), $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch { }
    }
}
