using System.Diagnostics;
using PlayStead.UI.Launching;

namespace PlayStead.UI.Tests.Launching;

public sealed class ShellExternalUriLauncherTests
{
    [Fact]
    public void Open_starts_uri_through_shell_execute()
    {
        ProcessStartInfo? captured =
            null;

        var launcher =
            new ShellExternalUriLauncher(
                startInfo =>
                {
                    captured =
                        startInfo;
                });

        launcher.Open(
            new Uri(
                "steam://rungameid/1874880",
                UriKind.Absolute));

        Assert.NotNull(
            captured);

        Assert.Equal(
            "steam://rungameid/1874880",
            captured.FileName);

        Assert.True(
            captured.UseShellExecute);
    }
}
