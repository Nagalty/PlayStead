using PlayStead.Platform.Paths;

namespace PlayStead.Platform.Tests.Paths;

public sealed class WindowsUserDataPathProviderTests
{
    [Fact]
    public void Get_roots_PlayStead_under_Windows_LocalApplicationData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.False(string.IsNullOrWhiteSpace(localAppData));

        var layout = new WindowsUserDataPathProvider().Get();

        Assert.Equal(
            Path.GetFullPath(Path.Combine(localAppData, "PlayStead")),
            layout.Root);
    }
}
