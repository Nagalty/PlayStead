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
    private IProviderObservedSessionStore? _providerSessionStore;
    private IEffectiveActivityService? _effectiveActivityService;
    private GameBuildHistoryService? _gameBuildHistoryService;

    private bool _hasSessionHistory;
    private string _lastActivityLabel = "J’ai encore peu de recul sur celui-là.";
    private string _lastSessionDateLabel = "—";
    private string _lastSessionDurationLabel = "—";
    private string _totalPlayTimeLabel = "Inconnu";
    private string _sessionCountLabel = "0 session connue";
    private string _knownSessionHistoryLabel = string.Empty;
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
        GameBuildHistoryService? gameBuildHistoryService = null,
        IProviderObservedSessionStore? providerSessionStore = null)
        : this(game, navigationService, launch)
    {
        _providerActivityStore = providerActivityStore;
        _gameBuildHistoryService = gameBuildHistoryService;
        _providerSessionStore = providerSessionStore;
    }

    public void AttachEffectiveActivityService(IEffectiveActivityService service) =>
        _effectiveActivityService = service;

    public GameQuickPanelViewModel(
        LibraryItemViewModel game,
        NavigationService navigationService,
        GameLaunchViewModel? launch,
        ISessionStore sessionStore,
        ISessionCorrectionStore sessionCorrectionStore,
        SessionCorrectionPolicy sessionCorrectionPolicy,
        IProviderActivityMetadataStore? providerActivityStore = null,
        GameBuildHistoryService? gameBuildHistoryService = null,
        IProviderObservedSessionStore? providerSessionStore = null)
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
        _providerSessionStore = providerSessionStore;
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
            OnPropertyChanged(nameof(HasNoActivity));
        }
    }

    public bool HasAnyActivity => HasSessionHistory || HasProviderActivity;

    public bool HasNoActivity => !HasAnyActivity;

    public bool HasAttention => _hasAttention;

    public bool HasSinceLastPlaySummary => HasAttention || _hasBuildChangeSinceLastPlay;

    public string SinceLastPlaySummary => HasAttention
        ? "Mise à jour disponible"
        : _hasBuildChangeSinceLastPlay
            ? "Il s’est passé quelque chose depuis ta dernière partie."
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

    public bool HasProviderPlayTime => ProviderPlayTimeLabel is not null;

    public string? ProviderLastPlayedLabel { get; private set; }

    public bool HasProviderLastPlayed => ProviderLastPlayedLabel is not null;

    public string? ProviderActivitySourceLabel { get; private set; }

    public bool HasProviderActivity =>
        ProviderPlayTimeLabel is not null || ProviderLastPlayedLabel is not null;

    public string SessionCountLabel
    {
        get => _sessionCountLabel;
        private set => SetField(ref _sessionCountLabel, value);
    }

    public string KnownSessionHistoryLabel
    {
        get => _knownSessionHistoryLabel;
        private set => SetField(ref _knownSessionHistoryLabel, value);
    }

    public bool HasKnownSessionHistory => !string.IsNullOrEmpty(KnownSessionHistoryLabel);

    public IReadOnlyList<RecentActivitySessionItemViewModel> RecentActivitySessions =>
        _recentActivitySessions;

    public bool HasRecentActivity =>
        _recentActivitySessions.Count > 0;

    public bool HasNoRecentActivity => !HasRecentActivity;

    public async Task LoadSessionSummaryAsync(
        CancellationToken cancellationToken)
    {
        await LoadProviderActivityAsync(cancellationToken);
        await LoadSinceLastPlaySummaryAsync(cancellationToken);

        var sessions = _sessionStore is null
            ? Array.Empty<GameSession>()
            : await _sessionStore.GetByGameAsync(Game.GameId.Value, cancellationToken);
        var providerSessions = _providerSessionStore is null
            ? Array.Empty<ProviderObservedSession>()
            : (await _providerSessionStore.GetByPeriodAsync(
                DateTimeOffset.UtcNow.AddYears(-10), DateTimeOffset.UtcNow, cancellationToken))
                .Where(item => item.GameId == Game.GameId &&
                    item.StartedAtUtc is not null &&
                    item.EndedAtUtc is not null &&
                    item.Completeness == ProviderObservedSessionCompleteness.Complete)
                .ToArray();

        if (sessions.Count == 0 && providerSessions.Length == 0)
        {
            SetRecentActivitySessions(Array.Empty<RecentActivitySessionItemViewModel>());
            HasSessionHistory = false;
            LastActivityLabel = "J’ai encore peu de recul sur celui-là.";
            LastSessionDateLabel = "—";
            LastSessionDurationLabel = "—";
            PlaySteadTotalPlayTimeLabel = "0 min";
            TotalPlayTimeLabel = ProviderPlayTimeLabel ?? "Inconnu";
            OnPropertyChanged(nameof(PlaySteadTotalPlayTimeLabel));
            SessionCountLabel = "0 session connue";
            SetKnownSessionHistory(null);
            return;
        }

        var resolved = new List<ResolvedSession>(sessions.Count);

        if (_sessionCorrectionStore is not null && _sessionCorrectionPolicy is not null)
        {
            foreach (var session in sessions)
            {
                var correction = await _sessionCorrectionStore.GetAsync(session.SessionId, cancellationToken);
                var effective = _sessionCorrectionPolicy.Resolve(session, correction);
                resolved.Add(new ResolvedSession(session, effective));
            }
        }

        var playSteadSegments = resolved
            .Where(item =>
                item.Session.GameId == Game.GameId.Value &&
                item.Session.State is SessionState.Ended or SessionState.Recovered &&
                item.Effective.EndedAtUtc is DateTimeOffset endedAt &&
                endedAt > item.Effective.StartedAtUtc)
            .Select(item => new ActivitySegment(item.Session.SessionId, item.Effective.StartedAtUtc, item.Effective.EndedAtUtc!.Value))
            .ToArray();
        var providerSegments = providerSessions
            .Select(item => new ActivitySegment(item.SessionId, item.StartedAtUtc!.Value, item.EndedAtUtc!.Value))
            .ToArray();
        var segments = MergeSegments(playSteadSegments.Concat(providerSegments));
        if (segments.Count == 0)
        {
            SetRecentActivitySessions(Array.Empty<RecentActivitySessionItemViewModel>());
            HasSessionHistory = false;
            LastActivityLabel = "J’ai encore peu de recul sur celui-là.";
            LastSessionDateLabel = "—";
            LastSessionDurationLabel = "—";
            PlaySteadTotalPlayTimeLabel = "0 min";
            TotalPlayTimeLabel = ProviderPlayTimeLabel ?? "Inconnu";
            OnPropertyChanged(nameof(PlaySteadTotalPlayTimeLabel));
            SessionCountLabel = "0 session connue";
            SetKnownSessionHistory(null);
            return;
        }
        var recentActivity = segments
            .OrderByDescending(item => item.StartedAtUtc)
            .ThenByDescending(item => item.SessionId)
            .Take(5)
            .Select(item => new RecentActivitySessionItemViewModel(item.SessionId, FormatTimestamp(item.StartedAtUtc), FormatDuration(item.EndedAtUtc - item.StartedAtUtc)))
            .ToArray();
        SetRecentActivitySessions(Array.AsReadOnly(recentActivity));

        var lastActivity = segments.Max(item => item.EndedAtUtc);
        var lastCompleted = segments.OrderByDescending(item => item.EndedAtUtc).ThenByDescending(item => item.SessionId).First();

        var total = segments.Aggregate(TimeSpan.Zero, (current, item) => current + (item.EndedAtUtc - item.StartedAtUtc));

        HasSessionHistory = true;
        LastActivityLabel =
            FormatTimestamp(
                lastActivity);

        LastSessionDateLabel = FormatTimestamp(lastCompleted.EndedAtUtc);

        LastSessionDurationLabel = FormatDuration(lastCompleted.EndedAtUtc - lastCompleted.StartedAtUtc);

        // Recovered provider sessions contribute to the activity timeline and
        // fallback total, but are not sessions observed directly by PlayStead.
        var playSteadTotal = playSteadSegments.Aggregate(
                TimeSpan.Zero,
                (current, item) => current + (item.EndedAtUtc - item.StartedAtUtc));
        PlaySteadTotalPlayTimeLabel = FormatDuration(playSteadTotal);
        TotalPlayTimeLabel = ProviderPlayTimeLabel ?? "Inconnu";
        OnPropertyChanged(nameof(PlaySteadTotalPlayTimeLabel));

        SessionCountLabel =
            segments.Count == 1
                ? "1 session connue"
                : $"{segments.Count} sessions connues";
        SetKnownSessionHistory(segments.Min(item => item.StartedAtUtc));

        if (_effectiveActivityService is not null)
        {
            var effective = await _effectiveActivityService.GetAsync(
                Game.GameId, Game.Provider, cancellationToken);
            TotalPlayTimeLabel = effective.EffectiveTotalPlayTime is { } totalPlaytime
                ? FormatDuration(totalPlaytime)
                : "Inconnu";
            LastActivityLabel = effective.EffectiveLastPlayedAtUtc is { } lastPlayed
                ? FormatTimestamp(lastPlayed)
                : "—";
            LastSessionDateLabel = LastActivityLabel;
            PlaySteadTotalPlayTimeLabel = FormatDuration(effective.PlaySteadObservedTime);
            SessionCountLabel = effective.EffectiveSessionCount == 1
                ? "1 session connue"
                : $"{effective.EffectiveSessionCount} sessions connues";
            SetKnownSessionHistory(effective.KnownSessionHistoryStartUtc);
            OnPropertyChanged(nameof(TotalPlayTimeLabel));
            OnPropertyChanged(nameof(LastActivityLabel));
            OnPropertyChanged(nameof(LastSessionDateLabel));
            OnPropertyChanged(nameof(PlaySteadTotalPlayTimeLabel));
            OnPropertyChanged(nameof(SessionCountLabel));
        }
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
        OnPropertyChanged(nameof(HasProviderPlayTime));
        OnPropertyChanged(nameof(ProviderLastPlayedLabel));
        OnPropertyChanged(nameof(HasProviderLastPlayed));
        OnPropertyChanged(nameof(ProviderActivitySourceLabel));
        OnPropertyChanged(nameof(HasProviderActivity));
        OnPropertyChanged(nameof(HasAnyActivity));
        OnPropertyChanged(nameof(HasNoActivity));
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

    private void SetKnownSessionHistory(DateTimeOffset? startUtc)
    {
        KnownSessionHistoryLabel = startUtc is { } start
            ? $"Historique connu depuis {FormatCoverageDate(start)}"
            : string.Empty;
        OnPropertyChanged(nameof(HasKnownSessionHistory));
    }

    private static string FormatCoverageDate(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("d MMM yyyy", CultureInfo.GetCultureInfo("fr-FR"));

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasNoRecentActivity)));
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

    private sealed record ActivitySegment(Guid SessionId, DateTimeOffset StartedAtUtc, DateTimeOffset EndedAtUtc);

    private static IReadOnlyList<ActivitySegment> MergeSegments(IEnumerable<ActivitySegment> source)
    {
        var merged = new List<ActivitySegment>();
        foreach (var segment in source.Where(item => item.EndedAtUtc > item.StartedAtUtc).OrderBy(item => item.StartedAtUtc))
        {
            if (merged.Count > 0 && segment.StartedAtUtc <= merged[^1].EndedAtUtc)
            {
                var previous = merged[^1];
                merged[^1] = previous with
                {
                    EndedAtUtc = previous.EndedAtUtc >= segment.EndedAtUtc ? previous.EndedAtUtc : segment.EndedAtUtc
                };
            }
            else
            {
                merged.Add(segment);
            }
        }
        return merged;
    }
}

public sealed record RecentActivitySessionItemViewModel(
    Guid SessionId,
    string StartedAtLabel,
    string DurationLabel);
