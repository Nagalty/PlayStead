using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using PlayStead.Core.Persistence;
using PlayStead.Core.Sessions;

namespace PlayStead.UI.Sessions;

public sealed class SessionViewModel :
    INotifyPropertyChanged
{
    private const int RecentSessionLimit = 50;

    private readonly ILibraryStore _libraryStore;
    private readonly SessionMonitor _sessionMonitor;
    private readonly TimeProvider _timeProvider;
    private readonly ISessionStore? _sessionStore;
    private readonly ISessionCorrectionStore? _sessionCorrectionStore;
    private readonly ISessionRuntime? _sessionRuntime;
    private readonly SessionCorrectionPolicy? _sessionCorrectionPolicy;

    private IReadOnlyDictionary<Guid, string> _gameTitles =
        new Dictionary<Guid, string>();

    private IReadOnlyList<ActiveSessionItemViewModel>
        _activeSessions =
            Array.Empty<ActiveSessionItemViewModel>();

    private IReadOnlyList<RecentSessionItemViewModel>
        _recentSessions =
            Array.Empty<RecentSessionItemViewModel>();

    private SessionDetailViewModel? _selectedSessionDetail;

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

    public SessionViewModel(
        ILibraryStore libraryStore,
        SessionMonitor sessionMonitor,
        TimeProvider timeProvider,
        ISessionStore sessionStore,
        ISessionCorrectionStore sessionCorrectionStore,
        ISessionRuntime sessionRuntime,
        SessionCorrectionPolicy sessionCorrectionPolicy)
        : this(
            libraryStore,
            sessionMonitor,
            timeProvider)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(sessionCorrectionStore);
        ArgumentNullException.ThrowIfNull(sessionRuntime);
        ArgumentNullException.ThrowIfNull(sessionCorrectionPolicy);

        _sessionStore = sessionStore;
        _sessionCorrectionStore = sessionCorrectionStore;
        _sessionRuntime = sessionRuntime;
        _sessionCorrectionPolicy = sessionCorrectionPolicy;
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

    public IReadOnlyList<RecentSessionItemViewModel>
        RecentSessions =>
            _recentSessions;

    public bool HasRecentSessions =>
        RecentSessions.Count > 0;

    public SessionDetailViewModel?
        SelectedSessionDetail =>
            _selectedSessionDetail;

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

        if (_sessionStore is not null &&
            _sessionCorrectionStore is not null &&
            _sessionCorrectionPolicy is not null)
        {
            await RefreshHistoryAsync(
                cancellationToken);
        }
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
                        ToActiveItem(
                            session,
                            nowUtc))
                .ToArray();
    }

    public async Task SelectRecentSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        EnsureHistoryDependencies();

        var session =
            await _sessionStore!.GetAsync(
                sessionId,
                cancellationToken);

        if (session is null)
        {
            SetSelectedSessionDetail(
                null);

            return;
        }

        var correction =
            await _sessionCorrectionStore!.GetAsync(
                sessionId,
                cancellationToken);

        SetSelectedSessionDetail(
            CreateDetail(
                session,
                correction));
    }

    private async Task RefreshHistoryAsync(
        CancellationToken cancellationToken)
    {
        var sessions =
            await _sessionStore!.GetRecentAsync(
                RecentSessionLimit,
                cancellationToken);

        var orderedSessions =
            sessions
                .OrderByDescending(
                    session =>
                        session.ObservedStartedAtUtc)
                .ThenByDescending(
                    session =>
                        session.SessionId)
                .ToArray();

        var items =
            new List<RecentSessionItemViewModel>(
                orderedSessions.Length);

        foreach (var session in orderedSessions)
        {
            var correction =
                await _sessionCorrectionStore!.GetAsync(
                    session.SessionId,
                    cancellationToken);

            var effective =
                _sessionCorrectionPolicy!.Resolve(
                    session,
                    correction);

            items.Add(
                ToRecentItem(
                    session,
                    effective));
        }

        SetRecentSessions(
            items);
    }

    public async Task RefreshRecentSessionsAsync(
        CancellationToken cancellationToken)
    {
        EnsureHistoryDependencies();
        await RefreshHistoryAsync(cancellationToken);
    }

    private SessionDetailViewModel CreateDetail(
        GameSession session,
        SessionCorrection? correction)
    {
        var correctionViewModel =
            new SessionCorrectionViewModel(
                session,
                correction,
                _sessionRuntime!,
                _sessionCorrectionPolicy!,
                _timeProvider,
                cancellationToken =>
                    RefreshAfterCorrectionAsync(
                        session.SessionId,
                        cancellationToken));

        return new SessionDetailViewModel(
            ResolveTitle(
                session.GameId),
            session,
            correction,
            _sessionCorrectionPolicy!,
            correctionViewModel);
    }

    private async Task RefreshAfterCorrectionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await RefreshHistoryAsync(
            cancellationToken);

        await SelectRecentSessionAsync(
            sessionId,
            cancellationToken);
    }

    private void SetRecentSessions(
        IReadOnlyList<RecentSessionItemViewModel> value)
    {
        _recentSessions = value;

        OnPropertyChanged(
            nameof(RecentSessions));

        OnPropertyChanged(
            nameof(HasRecentSessions));
    }

    private void SetSelectedSessionDetail(
        SessionDetailViewModel? value)
    {
        if (ReferenceEquals(
                _selectedSessionDetail,
                value))
        {
            return;
        }

        _selectedSessionDetail = value;

        OnPropertyChanged(
            nameof(SelectedSessionDetail));
    }

    private ActiveSessionItemViewModel ToActiveItem(
        GameSession session,
        DateTimeOffset nowUtc)
    {
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
            ResolveTitle(
                session.GameId),
            FormatTimestamp(
                session.ObservedStartedAtUtc),
            FormatDuration(
                elapsed));
    }

    private RecentSessionItemViewModel ToRecentItem(
        GameSession session,
        EffectiveSessionTime effective)
    {
        var elapsed =
            effective.EndedAtUtc is null
                ? TimeSpan.Zero
                : effective.EndedAtUtc.Value -
                  effective.StartedAtUtc;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return new RecentSessionItemViewModel(
            session.SessionId,
            session.GameId,
            ResolveTitle(
                session.GameId),
            FormatTimestamp(
                effective.StartedAtUtc),
            FormatDuration(
                elapsed),
            session.State ==
                SessionState.Recovered,
            effective.IsManuallyCorrected);
    }

    private string ResolveTitle(
        Guid gameId) =>
        _gameTitles.TryGetValue(
            gameId,
            out var knownTitle)
            ? knownTitle
            : "Jeu local";

    private static string FormatTimestamp(
        DateTimeOffset timestamp) =>
        timestamp
            .ToLocalTime()
            .ToString(
                "g",
                CultureInfo.CurrentCulture);

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

    private void EnsureHistoryDependencies()
    {
        if (_sessionStore is null ||
            _sessionCorrectionStore is null ||
            _sessionRuntime is null ||
            _sessionCorrectionPolicy is null)
        {
            throw new InvalidOperationException(
                "Session history dependencies are not configured.");
        }
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

public sealed record RecentSessionItemViewModel(
    Guid SessionId,
    Guid GameId,
    string Title,
    string StartedAtLabel,
    string DurationLabel,
    bool IsRecovered,
    bool IsCorrected);
