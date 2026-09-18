using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using PlayStead.Core.Sessions;
using PlayStead.UI.Launching;
using PlayStead.UI.Navigation;

namespace PlayStead.UI.Library;

public sealed class GameQuickPanelViewModel :
    INotifyPropertyChanged
{
    private readonly NavigationService _navigationService;
    private readonly ISessionStore? _sessionStore;
    private readonly ISessionCorrectionStore? _sessionCorrectionStore;
    private readonly SessionCorrectionPolicy? _sessionCorrectionPolicy;

    private bool _hasSessionHistory;
    private string _lastActivityLabel = "Aucune activité PlayStead";
    private string _lastSessionDateLabel = "—";
    private string _lastSessionDurationLabel = "—";
    private string _totalPlayTimeLabel = "0 min";
    private string _sessionCountLabel = "0 session";

    public GameQuickPanelViewModel(
        LibraryItemViewModel game,
        NavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(navigationService);

        Game = game;
        _navigationService = navigationService;
    }

    public GameQuickPanelViewModel(
        LibraryItemViewModel game,
        NavigationService navigationService,
        GameLaunchViewModel? launch)
        : this(game, navigationService)
    {
        Launch = launch;
    }

    public GameQuickPanelViewModel(
        LibraryItemViewModel game,
        NavigationService navigationService,
        GameLaunchViewModel? launch,
        ISessionStore sessionStore,
        ISessionCorrectionStore sessionCorrectionStore,
        SessionCorrectionPolicy sessionCorrectionPolicy)
        : this(game, navigationService, launch)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(sessionCorrectionStore);
        ArgumentNullException.ThrowIfNull(sessionCorrectionPolicy);

        _sessionStore = sessionStore;
        _sessionCorrectionStore = sessionCorrectionStore;
        _sessionCorrectionPolicy = sessionCorrectionPolicy;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LibraryItemViewModel Game { get; }

    public GameLaunchViewModel? Launch { get; }

    public bool HasSessionHistory
    {
        get => _hasSessionHistory;
        private set => SetField(ref _hasSessionHistory, value);
    }

    public string LastActivityLabel
    {
        get => _lastActivityLabel;
        private set => SetField(ref _lastActivityLabel, value);
    }

    public string LastSessionDateLabel
    {
        get => _lastSessionDateLabel;
        private set => SetField(ref _lastSessionDateLabel, value);
    }

    public string LastSessionDurationLabel
    {
        get => _lastSessionDurationLabel;
        private set => SetField(ref _lastSessionDurationLabel, value);
    }

    public string TotalPlayTimeLabel
    {
        get => _totalPlayTimeLabel;
        private set => SetField(ref _totalPlayTimeLabel, value);
    }

    public string SessionCountLabel
    {
        get => _sessionCountLabel;
        private set => SetField(ref _sessionCountLabel, value);
    }

    public async Task LoadSessionSummaryAsync(
        CancellationToken cancellationToken)
    {
        if (_sessionStore is null ||
            _sessionCorrectionStore is null ||
            _sessionCorrectionPolicy is null)
        {
            return;
        }

        var sessions =
            await _sessionStore.GetByGameAsync(
                Game.GameId.Value,
                cancellationToken);

        if (sessions.Count == 0)
        {
            HasSessionHistory = false;
            LastActivityLabel = "Aucune activité PlayStead";
            LastSessionDateLabel = "—";
            LastSessionDurationLabel = "—";
            TotalPlayTimeLabel = "0 min";
            SessionCountLabel = "0 session";
            return;
        }

        var resolved =
            new List<ResolvedSession>(
                sessions.Count);

        foreach (var session in sessions)
        {
            var correction =
                await _sessionCorrectionStore.GetAsync(
                    session.SessionId,
                    cancellationToken);

            var effective =
                _sessionCorrectionPolicy.Resolve(
                    session,
                    correction);

            resolved.Add(
                new ResolvedSession(
                    session,
                    effective));
        }

        var completed =
            resolved
                .Where(item =>
                    item.Effective.EndedAtUtc is not null &&
                    item.Effective.EndedAtUtc.Value >
                    item.Effective.StartedAtUtc)
                .ToArray();

        var lastActivity =
            resolved
                .Select(item =>
                    item.Effective.EndedAtUtc
                    ?? item.Session.LastSeenAtUtc)
                .Max();

        var lastCompleted =
            completed
                .OrderByDescending(item =>
                    item.Effective.EndedAtUtc)
                .ThenByDescending(item =>
                    item.Session.SessionId)
                .FirstOrDefault();

        var total =
            completed.Aggregate(
                TimeSpan.Zero,
                (current, item) =>
                    current +
                    (item.Effective.EndedAtUtc!.Value -
                     item.Effective.StartedAtUtc));

        HasSessionHistory = true;
        LastActivityLabel =
            FormatTimestamp(
                lastActivity);

        LastSessionDateLabel =
            lastCompleted is null
                ? "Aucune session terminée"
                : FormatTimestamp(
                    lastCompleted.Effective.EndedAtUtc!.Value);

        LastSessionDurationLabel =
            lastCompleted is null
                ? "—"
                : FormatDuration(
                    lastCompleted.Effective.EndedAtUtc!.Value -
                    lastCompleted.Effective.StartedAtUtc);

        TotalPlayTimeLabel =
            FormatDuration(total);

        SessionCountLabel =
            sessions.Count == 1
                ? "1 session"
                : $"{sessions.Count} sessions";
    }

    public void OpenDetails()
    {
        _navigationService.Navigate(
            new NavigationRequest(
                AppRoute.GameDetail,
                Game.GameId));
    }

    private static string FormatTimestamp(
        DateTimeOffset timestamp) =>
        FormatTimestampForDisplay(
            timestamp,
            DateTimeOffset.Now);

    public static string FormatTimestampForDisplay(
        DateTimeOffset timestamp,
        DateTimeOffset now)
    {
        var localTimestamp = timestamp.ToLocalTime();
        var localNow = now.ToLocalTime();
        var date = localTimestamp.Date;

        if (date == localNow.Date)
        {
            return $"aujourd’hui à {localTimestamp:HH:mm}";
        }

        if (date == localNow.Date.AddDays(-1))
        {
            return $"hier à {localTimestamp:HH:mm}";
        }

        var format = localTimestamp.Year == localNow.Year
            ? "d MMM 'à' HH:mm"
            : "d MMM yyyy 'à' HH:mm";

        return localTimestamp.ToString(
            format,
            CultureInfo.GetCultureInfo("fr-FR"));
    }

    private static string FormatDuration(
        TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        var totalHours =
            (int)duration.TotalHours;

        if (totalHours > 0)
        {
            return $"{totalHours} h {duration.Minutes:00} min";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes} min";
        }

        return duration > TimeSpan.Zero
            ? "< 1 min"
            : "0 min";
    }

    private void SetField<T>(
        ref T field,
        T value,
        [CallerMemberName]
        string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(
                field,
                value))
        {
            return;
        }

        field = value;

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }

    private sealed record ResolvedSession(
        GameSession Session,
        EffectiveSessionTime Effective);
}
