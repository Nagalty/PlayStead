using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PlayStead.Core.Persistence;

namespace PlayStead.UI.Library;

public partial class ManualGameDialog : Window
{
    public ManualGameDialog(
        ManualGameExecutableSelection? initialSelection = null,
        ManualGameDefinition? initialDefinition = null)
    {
        InitializeComponent();
        Loaded += (_, _) => TitleTextBox.Focus();

        if (initialSelection is not null)
            ApplySelection(initialSelection, overwriteOptionalFields: true);
        else if (initialDefinition is not null)
            ApplyDefinition(initialDefinition);
    }

    public ManualGameDefinition? Definition { get; private set; }

    public void SetError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }

    private void BrowseButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selection = ManualGameExecutablePicker.Select(this);
        if (selection is null)
            return;

        ApplySelection(selection, overwriteOptionalFields: false);
    }

    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Definition = ManualGameDefinition.Create(
                TitleTextBox.Text,
                ExecutableTextBox.Text,
                string.IsNullOrWhiteSpace(WorkingDirectoryTextBox.Text) ? null : WorkingDirectoryTextBox.Text,
                ArgumentsTextBox.Text);
            DialogResult = true;
        }
        catch (Exception exception)
        {
            SetError(exception.Message);
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Dialog_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void ApplySelection(ManualGameExecutableSelection selection, bool overwriteOptionalFields)
    {
        ExecutableTextBox.Text = selection.ExecutablePath;
        if (overwriteOptionalFields || string.IsNullOrWhiteSpace(TitleTextBox.Text))
            TitleTextBox.Text = selection.DisplayName;
        if (overwriteOptionalFields || string.IsNullOrWhiteSpace(WorkingDirectoryTextBox.Text))
            WorkingDirectoryTextBox.Text = selection.WorkingDirectory;
    }

    private void ApplyDefinition(ManualGameDefinition definition)
    {
        TitleTextBox.Text = definition.Title;
        ExecutableTextBox.Text = definition.ExecutablePath;
        WorkingDirectoryTextBox.Text = definition.WorkingDirectory;
        ArgumentsTextBox.Text = definition.LaunchArguments ?? string.Empty;
    }
}

public sealed record ManualGameExecutableSelection(
    string ExecutablePath,
    string WorkingDirectory,
    string DisplayName);

internal static class ManualGameExecutablePicker
{
    public const string DialogTitle = "Sélectionner l’exécutable du jeu";
    public const string DialogFilter = "Applications (*.exe;*.com)|*.exe;*.com|Tous les fichiers (*.*)|*.*";

    public static ManualGameExecutableSelection? Select(Window owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = DialogTitle,
            Filter = DialogFilter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog(owner) == true
            ? FromPath(dialog.FileName)
            : null;
    }

    private static ManualGameExecutableSelection FromPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var fallbackName = Path.GetFileNameWithoutExtension(fullPath);
        var displayName = fallbackName;

        try
        {
            var version = FileVersionInfo.GetVersionInfo(fullPath);
            displayName = FirstMeaningful(version.FileDescription, version.ProductName, fallbackName);
        }
        catch
        {
            // Metadata is optional; the selected path remains usable.
        }

        return new ManualGameExecutableSelection(fullPath, directory, displayName);
    }

    private static string FirstMeaningful(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))?.Trim()
        ?? string.Empty;
}
