using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class StartupProgressContractTests
{
    [Fact]
    public void State_trace_reports_begin_updated_progress_and_ready_without_changing_state_semantics()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var listener = new TextWriterTraceListener(output);
        var previousAutoFlush = Trace.AutoFlush;
        Trace.Listeners.Add(listener);
        Trace.AutoFlush = true;

        try
        {
            var state = new StartupProgressState();
            state.Begin();
            Assert.True(state.IsBusy);
            Assert.Equal(StartupStage.DiscoveringLibraries, state.Stage);

            state.Report(StartupStage.EnrichingCatalog, "Enrichissement du catalogue Steam", 18, 30);
            Assert.True(state.IsBusy);
            Assert.Equal(StartupStage.EnrichingCatalog, state.Stage);
            Assert.Equal(18, state.Current);
            Assert.Equal(30, state.Total);

            state.Ready();
            Assert.False(state.IsBusy);
            Assert.Equal(StartupStage.Ready, state.Stage);

            listener.Flush();
            var lines = output.ToString().Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
            Assert.Contains("[STARTUP-PROGRESS] BEGIN IsBusy=true", lines);
            Assert.Contains("[STARTUP-PROGRESS] Stage=EnrichingCatalog Message=\"Enrichissement du catalogue Steam\" Current=18 Total=30 IsIndeterminate=false", lines);
            Assert.Contains("[STARTUP-PROGRESS] READY IsBusy=false", lines);
            Assert.Contains(lines, line =>
                line.StartsWith("[STARTUP-FORENSIC] t=", StringComparison.Ordinal) &&
                line.Contains(" Thread=", StringComparison.Ordinal) &&
                line.Contains(" UI=false Event=Progress Stage=EnrichingCatalog Current=18 Total=30", StringComparison.Ordinal));
            Assert.True(Array.IndexOf(lines, "[STARTUP-PROGRESS] BEGIN IsBusy=true") < Array.FindIndex(lines, line => line.StartsWith("[STARTUP-PROGRESS] Stage=EnrichingCatalog", StringComparison.Ordinal)));
            Assert.True(Array.FindIndex(lines, line => line.StartsWith("[STARTUP-PROGRESS] Stage=Ready", StringComparison.Ordinal)) < Array.IndexOf(lines, "[STARTUP-PROGRESS] READY IsBusy=false"));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            Trace.AutoFlush = previousAutoFlush;
        }
    }

    [Fact]
    public void Unknown_and_known_counts_drive_indeterminate_and_honest_count_label()
    {
        var state = new StartupProgressState();

        state.Begin();

        Assert.True(state.IsIndeterminate);
        Assert.True(state.IsProgressIndeterminate);
        Assert.Null(state.Current);
        Assert.Null(state.Total);
        Assert.Empty(state.CountLabel);

        state.Report(StartupStage.EnrichingCatalog, "Enrichissement du catalogue Steam", 18, 30);

        Assert.False(state.IsIndeterminate);
        Assert.False(state.IsProgressIndeterminate);
        Assert.Equal(30, state.Total);
        Assert.Equal(18, state.Current);
        Assert.Equal("18 / 30 jeux", state.CountLabel);
    }

    [Fact]
    public void Startup_overlay_uses_shared_semantic_progress_style_and_current_bindings()
    {
        var controlsPath = FindRepositoryFile("src", "PlayStead.UI", "Themes", "PlaySteadControls.xaml");
        var controls = XDocument.Load(controlsPath);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var style = controls.Descendants(presentation + "Style")
            .SingleOrDefault(element => (string?)element.Attribute(xaml + "Key") == "PlayStead.ProgressBar.Startup");

        Assert.NotNull(style);
        Assert.Contains(style!.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Foreground" &&
            ((string?)setter.Attribute("Value"))?.Contains("PlayStead.Brush.Accent", StringComparison.Ordinal) == true);
        Assert.Contains(style.Elements(presentation + "Setter"), setter =>
            (string?)setter.Attribute("Property") == "Background" &&
            ((string?)setter.Attribute("Value"))?.Contains("PlayStead.Brush.SurfaceStrong", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(style.Elements(presentation + "Setter"), setter => (string?)setter.Attribute("Property") == "Template");

        var windowPath = FindRepositoryFile("src", "PlayStead.UI", "MainWindow.xaml");
        var window = XDocument.Load(windowPath);
        var progressBar = window.Descendants(presentation + "ProgressBar").Single();
        Assert.Contains(progressBar.Attributes(), attribute => attribute.Name.LocalName == "Style" &&
            attribute.Value.Contains("PlayStead.ProgressBar.Startup", StringComparison.Ordinal));
        Assert.Contains(progressBar.Attributes(), attribute => attribute.Name.LocalName == "IsIndeterminate" &&
            attribute.Value.Contains("StartupProgress.IsProgressIndeterminate", StringComparison.Ordinal));
        Assert.Contains(progressBar.Attributes(), attribute => attribute.Name.LocalName == "Maximum" &&
            attribute.Value.Contains("StartupProgress.Total", StringComparison.Ordinal));
        Assert.Contains(progressBar.Attributes(), attribute => attribute.Name.LocalName == "Value" &&
            attribute.Value.Contains("StartupProgress.Current", StringComparison.Ordinal));
    }

    private static string FindRepositoryFile(params string[] relativeParts)
    {
        for (var current = new DirectoryInfo(Directory.GetCurrentDirectory()); current is not null; current = current.Parent)
        {
            var candidate = Path.Combine([current.FullName, .. relativeParts]);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"Could not locate {Path.Combine(relativeParts)}");
    }
}
