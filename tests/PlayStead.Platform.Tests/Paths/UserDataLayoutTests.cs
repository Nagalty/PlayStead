using PlayStead.Platform.Paths;

namespace PlayStead.Platform.Tests.Paths;

public sealed class UserDataLayoutTests
{
    [Fact]
    public void FromRoot_builds_the_approved_local_data_layout()
    {
        var layout = UserDataLayout.FromRoot(@"C:\Users\Test\AppData\Local\PlayStead");

        Assert.Equal(@"C:\Users\Test\AppData\Local\PlayStead", layout.Root);
        Assert.Equal(@"C:\Users\Test\AppData\Local\PlayStead\Data\playstead.db", layout.DatabasePath);
        Assert.Equal(@"C:\Users\Test\AppData\Local\PlayStead\Cache", layout.CacheDirectory);
        Assert.Equal(@"C:\Users\Test\AppData\Local\PlayStead\Snapshots", layout.SnapshotsDirectory);
        Assert.Equal(@"C:\Users\Test\AppData\Local\PlayStead\Registry", layout.RegistryDirectory);
        Assert.Equal(@"C:\Users\Test\AppData\Local\PlayStead\Logs", layout.LogsDirectory);
        Assert.Equal(@"C:\Users\Test\AppData\Local\PlayStead\Backups", layout.BackupsDirectory);
    }
}
