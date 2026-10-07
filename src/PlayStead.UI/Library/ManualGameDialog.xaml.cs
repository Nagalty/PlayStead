using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using PlayStead.Core.Persistence;
using PlayStead.Core.Catalog;
using System.Collections.ObjectModel;

namespace PlayStead.UI.Library;

public partial class ManualGameDialog : Window
{
    private bool _suppressInstallRootEditTracking;
    private bool _installRootWasEdited;
    private readonly ICanonicalCatalogStore? _catalogStore;
    private CatalogContentId? _selectedCanonicalCatalogId;

    public ManualGameDialog(
        ManualGameExecutableSelection? initialSelection = null,
        ManualGameDefinition? initialDefinition = null,
        ICanonicalCatalogStore? catalogStore = null)
    {
        _catalogStore = catalogStore;
        InitializeComponent();
        var dialogTitle = initialDefinition is null ? "Ajouter un jeu" : "Modifier un jeu";
        Title = dialogTitle;
        DialogTitleTextBlock.Text = dialogTitle;
        SubmitButton.Content = initialDefinition is null ? "Ajouter" : "Enregistrer";
        Loaded += Dialog_OnLoaded;

        if (initialSelection is not null)
            ApplySelection(initialSelection, overwriteOptionalFields: true);
        else if (initialDefinition is not null)
            ApplyDefinition(initialDefinition);
    }

    public ManualGameDefinition? Definition { get; private set; }
    public ObservableCollection<CatalogContent> CatalogResults { get; } = [];
    public CatalogContent? SelectedCatalogContent { get; private set; }

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
                ArgumentsTextBox.Text,
                string.IsNullOrWhiteSpace(InstallRootPathTextBox.Text) ? null : InstallRootPathTextBox.Text,
                SelectedCatalogContent?.Id ?? _selectedCanonicalCatalogId);
            DialogResult = true;
        }
        catch (Exception exception)
        {
            SetError(exception.Message);
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private async void SearchCatalogButton_OnClick(object sender, RoutedEventArgs e)
    {
        CatalogResults.Clear();
        CatalogResultsList.Visibility = Visibility.Collapsed;
        if (_catalogStore is null || string.IsNullOrWhiteSpace(CatalogSearchTextBox.Text))
            return;

        try
        {
            var matches = await _catalogStore.FindByNormalizedTitleAsync(
                CanonicalCatalogTitleNormalizer.Normalize(CatalogSearchTextBox.Text),
                CancellationToken.None);
            foreach (var match in matches.Take(20))
                CatalogResults.Add(match);
            CatalogResultsList.Visibility = CatalogResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (CatalogResults.Count == 0)
                SetError("Aucun jeu correspondant dans le catalogue PlayStead.");
        }
        catch (Exception exception)
        {
            SetError(exception.Message);
        }
    }

    private void CatalogResult_OnSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SelectedCatalogContent = CatalogResultsList.SelectedItem as CatalogContent;
        if (SelectedCatalogContent is not null)
            SetAssociationCard(SelectedCatalogContent);
    }

    private void RemoveCatalogAssociationButton_OnClick(object sender, RoutedEventArgs e)
    {
        SelectedCatalogContent = null;
        _selectedCanonicalCatalogId = null;
        CatalogResultsList.SelectedItem = null;
        CatalogAssociationCard.Visibility = Visibility.Collapsed;
        CatalogEmptyAssociationTextBlock.Visibility = Visibility.Visible;
    }

    private void ModifyCatalogAssociationButton_OnClick(object sender, RoutedEventArgs e)
    {
        CatalogSearchTextBox.Focus();
        CatalogSearchTextBox.SelectAll();
    }

    private async void Dialog_OnLoaded(object? sender, RoutedEventArgs e)
    {
        TitleTextBox.Focus();
        if (_selectedCanonicalCatalogId is not null && _catalogStore is not null)
        {
            try
            {
                var content = await _catalogStore.GetByIdAsync(_selectedCanonicalCatalogId.Value, CancellationToken.None);
                if (content is not null)
                    SetAssociationCard(content);
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
            }
        }
    }

    private void SetAssociationCard(CatalogContent content)
    {
        SelectedCatalogContent = content;
        _selectedCanonicalCatalogId = content.Id;
        CatalogAssociationTitleTextBlock.Text = content.CanonicalTitle;
        CatalogAssociationCard.Visibility = Visibility.Visible;
        CatalogEmptyAssociationTextBlock.Visibility = Visibility.Collapsed;
        CatalogResultsList.Visibility = Visibility.Collapsed;
    }

    private void InstallRootBrowseButton_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choisir le dossier d’installation"
        };
        if (!string.IsNullOrWhiteSpace(InstallRootPathTextBox.Text) && Directory.Exists(InstallRootPathTextBox.Text))
            dialog.SelectedPath = InstallRootPathTextBox.Text;
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
            SetInstallRootText(dialog.SelectedPath, edited: true);
    }

    private void InstallRootPathTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_suppressInstallRootEditTracking)
            _installRootWasEdited = true;
    }

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
        WorkingDirectoryTextBox.Text = selection.WorkingDirectory;
        if (overwriteOptionalFields)
            _installRootWasEdited = false;
        if (overwriteOptionalFields || !_installRootWasEdited)
            SetInstallRootText(selection.InstallRootPath, edited: false);
    }

    private void ApplyDefinition(ManualGameDefinition definition)
    {
        TitleTextBox.Text = definition.Title;
        ExecutableTextBox.Text = definition.ExecutablePath;
        WorkingDirectoryTextBox.Text = definition.WorkingDirectory;
        SetInstallRootText(definition.InstallRootPath ?? definition.WorkingDirectory, edited: true);
        ArgumentsTextBox.Text = definition.LaunchArguments ?? string.Empty;
        _selectedCanonicalCatalogId = definition.CanonicalCatalogId;
        CatalogSearchTextBox.Text = string.Empty;
    }

    private void SetInstallRootText(string value, bool edited)
    {
        _suppressInstallRootEditTracking = true;
        try
        {
            InstallRootPathTextBox.Text = value;
            _installRootWasEdited = edited;
        }
        finally
        {
            _suppressInstallRootEditTracking = false;
        }
    }
}

public sealed record ManualGameExecutableSelection(
    string ExecutablePath,
    string WorkingDirectory,
    string DisplayName,
    string InstallRootPath);

internal static class ManualGamePrefill
{
    public static (string WorkingDirectory, string InstallRootPath) Build(string executablePath)
    {
        var fullPath = Path.GetFullPath(executablePath);
        var workingDirectory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        return (workingDirectory, ManualInstallRootHeuristics.Suggest(fullPath));
    }
}

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
        var (directory, installRootPath) = ManualGamePrefill.Build(fullPath);
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

        return new ManualGameExecutableSelection(fullPath, directory, displayName, installRootPath);
    }

    private static string FirstMeaningful(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))?.Trim()
        ?? string.Empty;
}
