using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Xml.Linq;
using PlayStead.UI.Settings;

namespace PlayStead.UI.Tests.Settings;

public sealed class Task03SettingsViewAndRouteTests
{
    [Fact]
    public void SettingsView_declares_reduce_motion_toggle_and_save_action()
    {
        var path =
            FindUiFile(
                "Settings/SettingsView.xaml");

        var document =
            XDocument.Load(
                path);

        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XNamespace xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        var root =
            Assert.IsType<XElement>(
                document.Root);

        Assert.Equal(
            presentation + "UserControl",
            root.Name);

        Assert.Equal(
            "PlayStead.UI.Settings.SettingsView",
            (string?)root.Attribute(
                xaml + "Class"));

        var toggle =
            Assert.Single(
                document.Descendants(
                    presentation + "CheckBox"),
                element =>
                    (string?)element.Attribute(
                        xaml + "Name")
                    == "ReduceMotionToggle");

        var checkedBinding =
            (string?)toggle.Attribute(
                "IsChecked");

        Assert.NotNull(
            checkedBinding);

        Assert.Contains(
            "Binding ReduceMotion",
            checkedBinding,
            StringComparison.Ordinal);

        Assert.Contains(
            "Mode=TwoWay",
            checkedBinding,
            StringComparison.Ordinal);

        var save =
            Assert.Single(
                document.Descendants(
                    presentation + "Button"),
                element =>
                    (string?)element.Attribute(
                        xaml + "Name")
                    == "SaveSettingsButton");

        var commandBinding =
            (string?)save.Attribute(
                "Command");

        Assert.NotNull(
            commandBinding);

        Assert.Contains(
            "Binding SaveCommand",
            commandBinding,
            StringComparison.Ordinal);

        var textValues =
            document
                .Descendants()
                .Attributes("Text")
                .Select(
                    attribute =>
                        attribute.Value)
                .ToArray();

        Assert.Contains(
            "Paramètres",
            textValues);

        Assert.Contains(
            textValues,
            value =>
                value.Contains(
                    "animations",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SettingsView_is_a_UserControl_constructed_with_SettingsViewModel()
    {
        var assembly =
            typeof(PlayStead.UI.Library.LibraryView)
                .Assembly;

        var type =
            assembly.GetType(
                "PlayStead.UI.Settings.SettingsView");

        Assert.NotNull(
            type);

        Assert.True(
            typeof(UserControl)
                .IsAssignableFrom(
                    type));

        var constructor =
            type.GetConstructor(
                BindingFlags.Public
                | BindingFlags.Instance,
                binder: null,
                [typeof(SettingsViewModel)],
                modifiers: null);

        Assert.NotNull(
            constructor);
    }

    [Fact]
    public void Host_registers_ui_preferences_store_and_SettingsViewModel()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    "Bootstrap/PlaySteadHost.cs"));

        Assert.Contains(
            "UiPreferencesStore",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "SettingsViewModel",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "ui-preferences.json",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_Settings_route_materializes_SettingsView_with_the_shared_view_model()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    "MainWindow.xaml.cs"));

        Assert.Contains(
            "PlayStead.UI.Settings",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "SettingsViewModel",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "SettingsView",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "case AppRoute.Settings:",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "new SettingsView(",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "MainContent.Content",
            source,
            StringComparison.Ordinal);
    }

    private static string FindUiFile(
        string relativePath)
    {
        var directory =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (directory is not null)
        {
            var uiDirectory =
                Path.Combine(
                    directory.FullName,
                    "src",
                    "PlayStead.UI");

            if (Directory.Exists(
                    uiDirectory))
            {
                var path =
                    Path.Combine(
                        uiDirectory,
                        relativePath);

                Assert.True(
                    File.Exists(
                        path),
                    $"Required UI file missing: {relativePath}");

                return path;
            }

            directory =
                directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "PlayStead.UI source directory was not found.");
    }
}
