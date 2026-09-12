namespace PlayStead.Platform.Paths;

public sealed class WindowsUserDataPathProvider
{
    public UserDataLayout Get()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("Windows LocalApplicationData path is unavailable.");
        }

        return UserDataLayout.FromRoot(Path.Combine(localAppData, "PlayStead"));
    }
}
