using System.IO;

namespace PlayStead.UI.Tests.Library;

public sealed class Task3CtaContractTests
{
    [Fact]
    public void Shared_play_button_uses_approved_primary_metrics_and_steam_action_is_secondary()
    {
        var root = FindRoot();
        var split = File.ReadAllText(Path.Combine(root, "src", "PlayStead.UI", "Controls", "PlaySplitButton.xaml"));
        var controls = File.ReadAllText(Path.Combine(root, "src", "PlayStead.UI", "Themes", "PlaySteadControls.xaml"));
        var detail = File.ReadAllText(Path.Combine(root, "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));

        Assert.Contains("Width=\"18\"", split);
        Assert.Contains("Height=\"18\"", split);
        Assert.Contains("PlayStead.Gap.Inline.ButtonIcon", split);
        Assert.Contains("PlayStead.Text.PrimaryButtonLabel", split);
        Assert.Contains("PlayStead.Brush.TextPrimary", split);
        Assert.DoesNotContain("#FFFFFF", split, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PlayStead.FontSize.16", controls);
        Assert.Contains("FontWeight\" Value=\"SemiBold\"", controls);
        Assert.Contains("PlayStead.Button.Secondary", detail);
        Assert.Contains("OpenProviderCommand", detail);
        Assert.Contains("PlayStead.Icon.External", detail);
        Assert.Contains("CanOpenProvider", detail);
        Assert.Contains("PlayStead.Gap.Inline.Cta", detail);
        Assert.Contains("Padding=\"20,14\"", detail);
        Assert.Contains("Width=\"18\" Height=\"18\"", detail);
    }

    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "PlayStead.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
