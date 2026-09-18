namespace PlayStead.UI.Tests.Library;

public sealed class PlaySplitButtonDisabledVisualTests
{
    [Fact]
    public void Primary_button_declares_PlayStead_disabled_visuals()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var splitButton = File.ReadAllText(Path.Combine(root, "src", "PlayStead.UI", "Controls", "PlaySplitButton.xaml"));
        var controls = File.ReadAllText(Path.Combine(root, "src", "PlayStead.UI", "Themes", "PlaySteadControls.xaml"));

        Assert.Contains("Style=\"{DynamicResource PlayStead.Button.Primary}\"", splitButton, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button.Style>", splitButton, StringComparison.Ordinal);

        var styleStart = controls.IndexOf("x:Key=\"PlayStead.Button.Primary\"", StringComparison.Ordinal);
        var styleEnd = controls.IndexOf("</Style>", styleStart, StringComparison.Ordinal);
        Assert.True(styleStart >= 0 && styleEnd > styleStart);
        var xaml = controls[styleStart..styleEnd];
        Assert.Contains("<Trigger Property=\"IsEnabled\" Value=\"False\">", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Brush.SurfaceStrong", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Brush.TextPrimary", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Brush.BorderStrong", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayLabel", splitButton, StringComparison.Ordinal);
    }
}
