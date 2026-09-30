using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class ManualGameDialogStructureTests
{
    private static string Read(string path) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "src", "PlayStead.UI", path));

    [Fact]
    public void Manual_game_dialog_uses_playstead_modal_and_controls()
    {
        var xaml = Read("Library/ManualGameDialog.xaml");

        Assert.Contains("WindowStyle=\"None\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Brush.Surface", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Brush.BorderStrong", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Parcourir…\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"BrowseButton_OnClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Button.Primary", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.Button.Secondary", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_game_browse_uses_filtered_file_picker_and_prefills_fields()
    {
        var code = Read("Library/ManualGameDialog.xaml.cs");

        Assert.Contains("new OpenFileDialog", code, StringComparison.Ordinal);
        Assert.Contains("Applications (*.exe;*.com)|*.exe;*.com", code, StringComparison.Ordinal);
        Assert.Contains("Sélectionner l’exécutable du jeu", code, StringComparison.Ordinal);
        Assert.Contains("CheckFileExists = true", code, StringComparison.Ordinal);
        Assert.Contains("Path.GetFileNameWithoutExtension(fullPath)", code, StringComparison.Ordinal);
        Assert.Contains("Path.GetDirectoryName(fullPath)", code, StringComparison.Ordinal);
        Assert.Contains("FileVersionInfo.GetVersionInfo", code, StringComparison.Ordinal);
        Assert.Contains("FileDescription", code, StringComparison.Ordinal);
        Assert.Contains("ProductName", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_game_flow_has_no_raw_win32_dialog()
    {
        var code = Read("Library/LibraryView.xaml.cs");

        var start = code.IndexOf("private async void AddManualGameButton_OnClick", StringComparison.Ordinal);
        var end = code.IndexOf("private void AttentionFilterButton_OnClick", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var manualFlow = code[start..end];

        Assert.Contains("new ManualGameDialog", manualFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageBox.Show", manualFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("Interaction.InputBox", manualFlow, StringComparison.Ordinal);
    }

    [Fact]
    public void Adding_a_manual_game_picks_before_opening_the_playstead_modal()
    {
        var code = Read("Library/LibraryView.xaml.cs");
        var start = code.IndexOf("private async void AddManualGameButton_OnClick", StringComparison.Ordinal);
        var end = code.IndexOf("private void AttentionFilterButton_OnClick", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var manualFlow = code[start..end];

        Assert.Contains("ManualGameExecutablePicker.Select", manualFlow, StringComparison.Ordinal);
        Assert.Contains("new ManualGameDialog(selection)", manualFlow, StringComparison.Ordinal);
        Assert.True(
            manualFlow.IndexOf("ManualGameExecutablePicker.Select", StringComparison.Ordinal)
            < manualFlow.IndexOf("new ManualGameDialog(selection)", StringComparison.Ordinal));
    }

    [Fact]
    public void Picker_cancel_returns_without_creating_or_persisting_a_game()
    {
        var code = Read("Library/LibraryView.xaml.cs");
        var start = code.IndexOf("private async void AddManualGameButton_OnClick", StringComparison.Ordinal);
        var end = code.IndexOf("private void AttentionFilterButton_OnClick", start, StringComparison.Ordinal);
        var manualFlow = code[start..end];

        Assert.Contains("if (selection is null)", manualFlow, StringComparison.Ordinal);
        Assert.True(
            manualFlow.IndexOf("if (selection is null)", StringComparison.Ordinal)
            < manualFlow.IndexOf("AddManualGameAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void Manual_game_detail_exposes_edit_and_non_destructive_remove_actions()
    {
        var xaml = Read("Library/GameDetailView.xaml");
        var mainWindow = Read("MainWindow.xaml.cs");

        Assert.Contains("Content=\"Modifier le jeu\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Retirer de PlayStead\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ManualGameRemovalDialog", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Aucun fichier du jeu ne sera supprimé", Read("Library/ManualGameRemovalDialog.xaml"), StringComparison.Ordinal);
    }
}
