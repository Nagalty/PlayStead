namespace PlayStead.UI.Tests.Library;

public sealed class PlaySplitButtonDisabledVisualTests
{
    [Fact]
    public void Primary_button_declares_PlayStead_disabled_visuals()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PlayStead.UI", "Controls", "PlaySplitButton.xaml");
        var xaml = File.ReadAllText(Path.GetFullPath(path));
        Assert.Contains("<Trigger Property=\"IsEnabled\" Value=\"False\">", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Brush.SurfaceStrong", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Brush.TextPrimary", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Brush.BorderStrong", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayLabel", xaml, StringComparison.Ordinal);
    }
}
