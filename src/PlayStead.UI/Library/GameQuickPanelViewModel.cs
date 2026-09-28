using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using PlayStead.Core.Sessions;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.GameBuildHistory;
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
    private IProviderActivityMetadataStore? _providerActivityStore;
    private GameBuildHistoryService? _gameBuildHistoryService;

    private bool _hasSessionHistory;
    private string _lastActivityLabel = "Aucune activité PlayStead";
    private string _lastSessionDateLabel = "—";
    private string _lastSessionDurationLabel = "—";
    private string _totalPlayTimeLabel = "0 min";
    private string _sessionCountLabel = "0 session";
    private IReadOnlyList<RecentActivitySessionItemViewModel> _recentActivitySessions =
        Array.Empty<RecentActivitySessionItemViewModel>();
    private bool _hasAttention;
    private bool _hasBuildChangeSinceLastPlay;

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
        IProviderActivityMetadataStore? providerActivityStore,
        GameBuildHistoryService? gameBuildHistoryService = null)
        : this(game, navigationService, launch)
    {
        _providerActivityStore = providerActivityStore;
        _gameBuildHistoryService = gameBuildHistoryService;
    }

    public GameQuickPanelViewModel(
        LibraryItemViewModel game,
        NavigationService navigationService,
        GameLaunchViewModel? launch,
        ISessionStore sessionStore,
        ISessionCorrectionStore sessionCorrectionStore,
        SessionCorrectionPolicy sessionCorrectionPolicy,
        IProviderActivityMetadataStore? providerActivityStore = null,
        GameBuildHistoryService? gameBuildHistoryService = null)
        : this(game, navigationService, launch)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(sessionCorrectionStore);
        ArgumentNullException.ThrowIfNull(sessionCorrectionPolicy);

        _sessionStore = sessionStore;
        _sessionCorrectionStore = sessionCorrectionStore;
        _sessionCorrectionPolicy = sessionCorrectionPolicy;
        _providerActivityStore = providerActivityStore;
        _gameBuildHistoryService = gameBuildHistoryService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public LibraryItemViewModel Game { get; }

    public GameLaunchViewModel? Launch { get; }

    public bool HasSessionHistory
    {
        get => _hasSessionHistory;
        private set
        {
            if (_hasSessionHistory == value)
                return;
            _hasSessionHistory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasAnyActivity));
        }
    }

    public bool HasAnyActivity => HasSessionHistory || HasProviderActivity;

    public bool HasAttention => _hasAttention;

    public bool HasSinceLastPlaySummary => HasAttention || _hasBuildChangeSinceLastPlay;

    public string SinceLastPlaySummary => HasAttention
        ? "Mise à jour disponible"
        : _hasBuildChangeSinceLastPlay
            ? "Build modifié depuis ta dernière session"
            : string.Empty;

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

    public string PlaySteadTotalPlayTimeLabel { get; private set; } = "0 min";

    public string? ProviderPlayTimeLabel { get; private set; }

    public string? ProviderLastPlayedLabel { get; private set; }

    public string? ProviderActivitySourceLabel { get; private set; }

    public bool HasProviderActivity =>
        ProviderPlayTimeLabel is not null || ProviderLastPlayedLabel is not null;

    public string SessionCountLabel
    {
        get => _sessionCountLabel;
        private set => SetField(ref _sessionCountLabel, value);
    }

    public IReadOnlyList<RecentActivitySessionItemViewModel> RecentActivitySessions =>
        _recentActivitySessions;

    public bool HasRecentActivity =>
        _recentActivitySessions.Count > 0;

    public async Task LoadSessionSummaryAsync(
        CancellationToken cancellationToken)
    {
        await LoadProviderActivityAsync(cancellationToken);
        await LoadSinceLastPlaySummaryAsync(cancellationToken);

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
            SetRecentActivitySessions(Array.Empty<RecentActivitySessionItemViewModel>());
            HasSessionHistory = false;
            LastActivityLabel = "Aucune activité PlayStead";
            LastSessionDateLabel = "—";
            LastSessionDurationLabel = "—";
            PlaySteadTotalPlayTimeLabel = "0 min";
            TotalPlayTimeLabel = ProviderPlayTimeLabel ?? PlaySteadTotalPlayTimeLabel;
            OnPropertyChanged(nameof(PlaySteadTotalPlayTimeLabel));
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

        var recentActivity = resolved
            .Where(item =>
                item.Session.GameId == Game.GameId.Value &&
                item.Session.State is SessionState.Ended or SessionState.Recovered &&
                item.Effective.EndedAtUtc is DateTimeOffset endedAt &&
                endedAt > item.Effective.StartedAtUtc)
            .GroupBy(item => item.Session.SessionId)
            .Select(group => group
                .OrderByDescending(item => item.Effective.StartedAtUtc)
                .First())
            .OrderByDescending(item => item.Effective.StartedAtUtc)
            .ThenByDescending(item => item.Session.SessionId)
            .Take(5)
            .Select(item => new RecentActivitySessionItemViewModel(
                item.Session.SessionId,
                FormatTimestamp(item.Effective.StartedAtUtc),
                FormatDuration(item.Effective.EndedAtUtc!.Value - item.Effective.StartedAtUtc)))
            .ToArray();
        SetRecentActivitySessions(Array.AsReadOnly(recentActivity));

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

        PlaySteadTotalPlayTimeLabel = FormatDuration(total);
        TotalPlayTimeLabel = ProviderPlayTimeLabel ?? PlaySteadTotalPlayTimeLabel;
        OnPropertyChanged(nameof(PlaySteadTotalPlayTimeLabel));

        SessionCountLabel =
            sessions.Count == 1
                ? "1 session"
                : $"{sessions.Count} sessions";
    }

    public void SetAttentionState(bool hasAttention)
    {
        if (_hasAttention == hasAttention)
            return;

        _hasAttention = hasAttention;
        OnPropertyChanged(nameof(HasAttention));
        OnPropertyChanged(nameof(HasSinceLastPlaySummary));
        OnPropertyChanged(nameof(SinceLastPlaySummary));
    }

    private async Task LoadSinceLastPlaySummaryAsync(CancellationToken cancellationToken)
    {
        _hasBuildChangeSinceLastPlay = _gameBuildHistoryService is not null &&
            await _gameBuildHistoryService.HasChangedSinceLastPlayAsync(
                Game.GameId,
                Game.Provider,
                cancellationToken);
        OnPropertyChanged(nameof(HasSinceLastPlaySummary));
        OnPropertyChanged(nameof(SinceLastPlaySummary));
    }

    private async Task LoadProviderActivityAsync(CancellationToken cancellationToken)
    {
        if (_providerActivityStore is null)
        {
            return;
        }

        var metadata = (await _providerActivityStore.GetAllAsync(cancellationToken))
            .FirstOrDefault(item => item.GameId == Game.GameId && item.Provider == Game.Provider);

        ProviderPlayTimeLabel = metadata?.TotalPlaytime is { } playtime
            ? FormatProviderDuration(playtime)
            : null;
        ProviderLastPlayedLabel = metadata?.LastPlayedAtUtc is { } lastPlayed
            ? FormatTimestamp(lastPlayed)
            : null;
        ProviderActivitySourceLabel = metadata?.Source.ToString();
        OnPropertyChanged(nameof(ProviderPlayTimeLabel));
        OnPropertyChanged(nameof(ProviderLastPlayedLabel));
        OnPropertyChanged(nameof(ProviderActivitySourceLabel));
        OnPropertyChanged(nameof(HasProviderActivity));
        OnPropertyChanged(nameof(HasAnyActivity));
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
            UiDisplayCulture.Current);
    }

    private void SetRecentActivitySessions(IReadOnlyList<RecentActivitySessionItemViewModel> value)
    {
        _recentActivitySessions = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RecentActivitySessions)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasRecentActivity)));
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

    private static string FormatProviderDuration(TimeSpan duration)
    {
        var totalHours = (int)duration.TotalHours;
        return duration.Minutes == 0
            ? $"{totalHours} h"
            : $"{totalHours} h {duration.Minutes:00} min";
    }

    internal static string FormatDurationForDisplay(
        TimeSpan duration) =>
        FormatDuration(duration);

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed record ResolvedSession(
        GameSession Session,
        EffectiveSessionTime Effective);
}

public sealed record RecentActivitySessionItemViewModel(
    Guid SessionId,
    string StartedAtLabel,
    string DurationLabel);
