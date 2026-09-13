using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using PlayStead.Core.Persistence;
using PlayStead.Core.Sessions;

namespace PlayStead.UI.Sessions;

public sealed class SessionViewModel :
    INotifyPropertyChanged
{
    private readonly ILibraryStore _libraryStore;
    private readonly SessionMonitor _sessionMonitor;
    private readonly TimeProvider _timeProvider;

    private IReadOnlyDictionary<Guid, string> _gameTitles =
        new Dictionary<Guid, string>();

    private IReadOnlyList<ActiveSessionItemViewModel>
        _activeSessions =
            Array.Empty<ActiveSessionItemViewModel>();

    public SessionViewModel(
        ILibraryStore libraryStore,
        SessionMonitor sessionMonitor,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(libraryStore);
        ArgumentNullException.ThrowIfNull(sessionMonitor);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _libraryStore = libraryStore;
        _sessionMonitor = sessionMonitor;
        _timeProvider = timeProvider;
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public IReadOnlyList<ActiveSessionItemViewModel>
        ActiveSessions
    {
        get => _activeSessions;
        private set
        {
            _activeSessions = value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasActiveSessions));
        }
    }

    public bool HasActiveSessions =>
        ActiveSessions.Count > 0;

    public async Task RefreshAsync(
        CancellationToken cancellationToken)
    {
        var library =
            await _libraryStore.LoadSnapshotAsync(
                cancellationToken);

        _gameTitles =
            library.Games
                .ToDictionary(
                    game => game.Id.Value,
                    game => game.Title);

        RefreshLive();
    }

    public void RefreshLive()
    {
        var runtimeSnapshot =
            _sessionMonitor.LatestSnapshot;

        if (runtimeSnapshot is null)
        {
            ActiveSessions =
                Array.Empty<ActiveSessionItemViewModel>();

            return;
        }

        var nowUtc =
            _timeProvider.GetUtcNow();

        ActiveSessions =
            runtimeSnapshot.ActiveSessions
                .OrderBy(
                    session =>
                        session.ObservedStartedAtUtc)
                .ThenBy(
                    session =>
                        session.SessionId)
                .Select(
                    session =>
                        ToItem(
                            session,
                            nowUtc))
                .ToArray();
    }

    private ActiveSessionItemViewModel ToItem(
        GameSession session,
        DateTimeOffset nowUtc)
    {
        var title =
            _gameTitles.TryGetValue(
                session.GameId,
                out var knownTitle)
                ? knownTitle
                : "Jeu local";

        var elapsed =
            nowUtc -
            session.ObservedStartedAtUtc;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return new ActiveSessionItemViewModel(
            session.SessionId,
            session.GameId,
            title,
            session.ObservedStartedAtUtc
                .ToLocalTime()
                .ToString(
                    "g",
                    CultureInfo.CurrentCulture),
            FormatDuration(
                elapsed));
    }

    private static string FormatDuration(
        TimeSpan elapsed)
    {
        var totalHours =
            (int)elapsed.TotalHours;

        return totalHours > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{totalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{elapsed.Minutes:00}:{elapsed.Seconds:00}");
    }

    private void OnPropertyChanged(
        [CallerMemberName]
        string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
}

public sealed record ActiveSessionItemViewModel(
    Guid SessionId,
    Guid GameId,
    string Title,
    string StartedAtLabel,
    string DurationLabel);
