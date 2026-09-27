using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PlayStead.UI.Bootstrap;

public enum StartupStage { DiscoveringLibraries, ScanningGames, EnrichingCatalog, RefreshingLibrary, Ready }

public sealed class StartupProgressState : INotifyPropertyChanged
{
    private StartupStage _stage;
    private string _message = "Préparation de PlayStead";
    private int? _current;
    private int? _total;
    private bool _isBusy;
    public event PropertyChangedEventHandler? PropertyChanged;
    public StartupStage Stage { get => _stage; private set { _stage = value; Changed(); } }
    public string Message { get => _message; private set { _message = value; Changed(); } }
    public int? Current { get => _current; private set { _current = value; Changed(); Changed(nameof(HasCount)); Changed(nameof(IsIndeterminate)); Changed(nameof(IsProgressIndeterminate)); Changed(nameof(CountLabel)); } }
    public int? Total { get => _total; private set { _total = value; Changed(); Changed(nameof(HasCount)); Changed(nameof(IsIndeterminate)); Changed(nameof(IsProgressIndeterminate)); Changed(nameof(CountLabel)); } }
    public bool HasCount => Current.HasValue && Total.HasValue;
    public bool IsIndeterminate => !HasCount;
    public bool IsProgressIndeterminate => IsBusy && IsIndeterminate;
    public string CountLabel => HasCount ? $"{Current} / {Total} jeux" : string.Empty;
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; Changed(); Changed(nameof(IsProgressIndeterminate)); } }
    public void Begin() { StartupForensicTrace.Restart(); IsBusy = true; Trace.WriteLine("[STARTUP-PROGRESS] BEGIN IsBusy=true"); Report(StartupStage.DiscoveringLibraries, "Détection des bibliothèques", null, null); }
    public void Report(StartupStage stage, string message, int? current, int? total) { if (current is not null && total is not null && current > total) throw new ArgumentOutOfRangeException(nameof(current)); Stage = stage; Message = message; Current = current; Total = total; Trace.WriteLine($"[STARTUP-PROGRESS] Stage={Stage} Message=\"{Message}\" Current={FormatCount(Current)} Total={FormatCount(Total)} IsIndeterminate={IsIndeterminate.ToString().ToLowerInvariant()}"); StartupForensicTrace.WriteProgress(Stage, Current, Total); }
    public void Ready() { Report(StartupStage.Ready, "Prêt", null, null); IsBusy = false; Trace.WriteLine("[STARTUP-PROGRESS] READY IsBusy=false"); }
    private static string FormatCount(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "null";
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

internal static class StartupForensicTrace
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    internal static void Restart() => Clock.Restart();

    internal static void Write(string eventName)
    {
        var onUiThread = Application.Current?.Dispatcher.CheckAccess() == true;
        Trace.WriteLine($"[STARTUP-FORENSIC] t={Clock.ElapsedMilliseconds} Thread={Environment.CurrentManagedThreadId} UI={onUiThread.ToString().ToLowerInvariant()} Event={eventName}");
    }

    internal static void WriteProgress(StartupStage stage, int? current, int? total)
    {
        var currentText = current?.ToString(CultureInfo.InvariantCulture) ?? "null";
        var totalText = total?.ToString(CultureInfo.InvariantCulture) ?? "null";
        Write($"Progress Stage={stage} Current={currentText} Total={totalText}");
    }

    internal static async Task MeasureAsync(string phase, Func<Task> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        Trace.WriteLine($"[STARTUP-TIMING] START Phase={phase}");
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            Trace.WriteLine($"[STARTUP-TIMING] END Phase={phase} DurationMs={stopwatch.ElapsedMilliseconds}");
        }
    }
}
