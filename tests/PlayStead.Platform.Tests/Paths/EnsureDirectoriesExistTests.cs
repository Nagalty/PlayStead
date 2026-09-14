using PlayStead.Platform.Paths;

namespace PlayStead.Platform.Tests.Paths;

public sealed class EnsureDirectoriesExistTests
{
    [Fact]
    public void EnsureDirectoriesExist_creates_the_approved_directory_tree()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var layout = UserDataLayout.FromRoot(root);

            layout.EnsureDirectoriesExist();

            Assert.True(Directory.Exists(Path.GetDirectoryName(layout.DatabasePath)));
            Assert.True(Directory.Exists(layout.CacheDirectory));
            Assert.True(Directory.Exists(layout.SnapshotsDirectory));
            Assert.True(Directory.Exists(layout.RegistryDirectory));
            Assert.True(Directory.Exists(layout.LogsDirectory));
            Assert.True(Directory.Exists(layout.BackupsDirectory));
            Assert.True(Directory.Exists(layout.MediaDirectory));
            Assert.False(File.Exists(layout.DatabasePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
