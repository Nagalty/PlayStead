using System.Xml.Linq;
using PlayStead.Core.Library;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryProtectionBadgeTests
{
    [Fact]
    public void Library_item_exposes_protection_state_and_accessible_tooltip()
    {
        var unprotected = new LibraryItemViewModel(
            GameId.New(),
            "Game",
            ProviderKind.Steam,
            "Steam",
            @"C:\Games\Game",
            null);
        var protectedItem = unprotected with { IsLocallyProtected = true };

        Assert.False(unprotected.IsLocallyProtected);
        Assert.True(protectedItem.IsLocallyProtected);
        Assert.Equal("Protégé par PlayStead", protectedItem.ProtectionBadgeTooltip);
    }

    [Fact]
    public void Grid_card_declares_a_hidden_by_default_protection_badge()
    {
        var document = LoadUi("Controls/GameCard.xaml");
        var badge = document.Descendants().Single(element =>
            (string?)element.Attribute("ToolTip") == "{Binding ProtectionBadgeTooltip}");

        Assert.Equal(
            "{Binding IsLocallyProtected, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)badge.Attribute("Visibility"));
        Assert.Contains("Protégé par PlayStead", document.ToString(), StringComparison.Ordinal);
        Assert.Contains("Copper", document.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void List_view_declares_the_same_protection_badge_contract()
    {
        var document = LoadUi("Library/LibraryView.xaml");
        var badge = document.Descendants().Single(element =>
            (string?)element.Attribute("ToolTip") == "{Binding ProtectionBadgeTooltip}");

        Assert.Equal(
            "{Binding IsLocallyProtected, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)badge.Attribute("Visibility"));
        Assert.Contains("Protégé par PlayStead", document.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Library_projection_uses_the_existing_protection_service_state()
    {
        var source = FindUiFile(Path.Combine("Library", "LibraryViewModel.cs"));
        var text = File.ReadAllText(source);

        Assert.Contains("ILocalProtectionSetupService", text, StringComparison.Ordinal);
        Assert.Contains("LocalProtectionState.Protected", text, StringComparison.Ordinal);
        Assert.Contains("_locallyProtectedGameIds", text, StringComparison.Ordinal);
        Assert.Contains("!_protectionOverrides.TryGetValue(gameId.Value, out var enabled) || enabled", text, StringComparison.Ordinal);
    }

    private static XDocument LoadUi(string relativePath) => XDocument.Load(FindUiFile(relativePath));

    private static string FindUiFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "PlayStead.UI", relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
