using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class QuickPanelHeaderContractTests
{
    [Fact]
    public void Quick_panel_header_keeps_provider_only_and_installation_keeps_size()
    {
        var root = FindRoot();
        var xaml = XElement.Load(Path.Combine(root, "src/PlayStead.UI/Library/LibraryView.xaml"));
        var text = xaml.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("Text=\"{Binding ProviderLabel}\"", text);
        Assert.DoesNotContain("Text=\"·\"", text);
        Assert.Contains("Text=\"{Binding InstalledSizeLabel, Mode=OneWay}\"", text);
        Assert.Contains("Text=\"Taille\"", text);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
