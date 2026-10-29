namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailLocalEnvironmentStructureTests
{
    [Fact]
    public void Game_detail_declares_compact_local_environment_section_without_provider_specific_markup()
    {
        var root = FindRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "PlayStead.UI", "Library", "LocalEnvironmentSectionViewModel.cs"));

        Assert.Contains("x:Name=\"LocalEnvironmentSection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Title=\"Environnement local\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("LocalEnvironment.IsVisible", xaml, StringComparison.Ordinal);
        Assert.Contains("LocalEnvironment.ConfigSummary", xaml, StringComparison.Ordinal);
        Assert.Contains("Configs, sauvegardes, logs et composants détectés", xaml, StringComparison.Ordinal);
        Assert.Contains("Configurations", xaml, StringComparison.Ordinal);
        Assert.Contains("Voir les changements", xaml, StringComparison.Ordinal);
        Assert.Contains("Changements de configuration", xaml, StringComparison.Ordinal);
        Assert.Contains("LocalEnvironment.ConfigChanges.Changes", xaml, StringComparison.Ordinal);
        Assert.Contains("RestoreSelectedChangesCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsSelected, Mode=TwoWay}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("PreRestore", xaml, StringComparison.Ordinal);
        Assert.Contains("Sauvegardes", xaml, StringComparison.Ordinal);
        Assert.Contains("Aucune donnée locale connue pour cette installation.", xaml, StringComparison.Ordinal);
        Assert.Contains("LocalEnvironment.HasEmptyState", xaml, StringComparison.Ordinal);
        Assert.Contains("LocalEnvironment.SetBaselineCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Title=\"Fichiers locaux\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Aucune donnée locale connue pour cette installation.", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderKind.Steam", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderKind.Epic", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderKind.Gog", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Local_environment_uses_player_facing_protection_language()
    {
        var root = FindRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "PlayStead.UI", "Library", "LocalEnvironmentSectionViewModel.cs"));

        Assert.Contains("Protection des réglages", xaml, StringComparison.Ordinal);
        Assert.Contains("Protéger maintenant", xaml, StringComparison.Ordinal);
        Assert.Contains("Utiliser cet état comme référence", xaml, StringComparison.Ordinal);
        Assert.Contains("Avant une mise à jour", xaml, StringComparison.Ordinal);
        Assert.Contains("BeforeUpdatePolicyOptions", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.ComboBox.Dark", xaml, StringComparison.Ordinal);
        Assert.Contains("PlayStead.ComboBox.PolicyItemTemplate", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedValue=\"{Binding LocalEnvironment.BeforeUpdatePolicy, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BeforeUpdatePolicyComboBox_SelectionChanged", xaml, StringComparison.Ordinal);
        Assert.Contains("Créée manuellement", viewModel, StringComparison.Ordinal);
        Assert.Contains("État avant restauration", viewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("PreRestore", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SHA-256", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Snapshot\"", xaml, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
