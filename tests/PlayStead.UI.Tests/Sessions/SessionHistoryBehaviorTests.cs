using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class SessionHistoryBehaviorTests
{
    [Fact]
    [Trait("Task11Cycle", "BHistory")]
    public async Task B12_Refresh_loads_recent_sessions_from_store_and_projects_history_items()
    {
        var session = Session(
            sessionId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            gameId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            start: Utc(10),
            end: Utc(11));

        var fixture = CreateFixture([session]);

        await fixture.Sut.RefreshAsync(
            CancellationToken.None);

        Assert.Equal(1, fixture.SessionStore.GetRecentCallCount);

        var item =
            Assert.Single(
                fixture.Sut.RecentSessions);

        Assert.Equal(session.SessionId, item.SessionId);
        Assert.Equal(session.GameId, item.GameId);
        Assert.Equal("Jeu local", item.Title);
        Assert.Equal(Label(Utc(10)), item.StartedAtLabel);
        Assert.Equal("1:00:00", item.DurationLabel);
        Assert.False(item.IsRecovered);
        Assert.False(item.IsCorrected);
        Assert.True(fixture.Sut.HasRecentSessions);
    }

    [Fact]
    [Trait("Task11Cycle", "BHistory")]
    public async Task B13_Refresh_orders_recent_sessions_newest_first()
    {
        var older = Session(
            sessionId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            gameId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            start: Utc(8),
            end: Utc(9));

        var newest = Session(
            sessionId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            gameId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            start: Utc(12),
            end: Utc(13));

        var middle = Session(
            sessionId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            gameId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            start: Utc(10),
            end: Utc(11));

        var fixture =
            CreateFixture(
                [older, newest, middle]);

        await fixture.Sut.RefreshAsync(
            CancellationToken.None);

        Assert.Equal(
            [
                newest.SessionId,
                middle.SessionId,
                older.SessionId
            ],
            fixture.Sut.RecentSessions
                .Select(item => item.SessionId)
                .ToArray());
    }

    [Fact]
    [Trait("Task11Cycle", "BHistory")]
    public async Task B13_Refresh_updates_empty_state_and_notifies_recent_projection()
    {
        var session = Session(
            sessionId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            gameId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            start: Utc(10),
            end: Utc(11));

        var fixture =
            CreateFixture([session]);

        var changed =
            new List<string?>();

        fixture.Sut.PropertyChanged +=
            (_, args) =>
                changed.Add(
                    args.PropertyName);

        await fixture.Sut.RefreshAsync(
            CancellationToken.None);

        Assert.True(
            fixture.Sut.HasRecentSessions);

        Assert.Contains(
            nameof(SessionViewModel.RecentSessions),
            changed);

        Assert.Contains(
            nameof(SessionViewModel.HasRecentSessions),
            changed);

        changed.Clear();
        fixture.SessionStore.Recent = [];

        await fixture.Sut.RefreshAsync(
            CancellationToken.None);

        Assert.Empty(
            fixture.Sut.RecentSessions);

        Assert.False(
            fixture.Sut.HasRecentSessions);

        Assert.Contains(
            nameof(SessionViewModel.RecentSessions),
            changed);

        Assert.Contains(
            nameof(SessionViewModel.HasRecentSessions),
            changed);
    }

    [Fact]
    [Trait("Task11Cycle", "BHistory")]
    public async Task B15_Refresh_applies_effective_correction_and_recovery_to_history_projection()
    {
        var corrected = Session(
            sessionId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            gameId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            start: Utc(10),
            end: Utc(11));

        var recovered = Session(
            sessionId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            gameId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            start: Utc(8),
            end: Utc(9),
            state: SessionState.Recovered,
            endReason:
                SessionEndReason
                    .RecoveredAfterUnexpectedShutdown);

        var fixture =
            CreateFixture(
                [recovered, corrected]);

        fixture.CorrectionStore.Set(
            new SessionCorrectionPolicy()
                .Create(
                    corrected,
                    new SessionCorrectionRequest(
                        corrected.SessionId,
                        Utc(10, 15),
                        Utc(11, 45),
                        "Correction historique"),
                    Utc(14)));

        await fixture.Sut.RefreshAsync(
            CancellationToken.None);

        var correctedItem =
            Assert.Single(
                fixture.Sut.RecentSessions,
                item =>
                    item.SessionId ==
                    corrected.SessionId);

        Assert.Equal(
            Label(Utc(10, 15)),
            correctedItem.StartedAtLabel);

        Assert.Equal(
            "1:30:00",
            correctedItem.DurationLabel);

        Assert.True(
            correctedItem.IsCorrected);

        Assert.False(
            correctedItem.IsRecovered);

        var recoveredItem =
            Assert.Single(
                fixture.Sut.RecentSessions,
                item =>
                    item.SessionId ==
                    recovered.SessionId);

        Assert.True(
            recoveredItem.IsRecovered);

        Assert.False(
            recoveredItem.IsCorrected);

        Assert.Contains(
            corrected.SessionId,
            fixture.CorrectionStore.GetCalls);

        Assert.Contains(
            recovered.SessionId,
            fixture.CorrectionStore.GetCalls);
    }

    [Fact]
    [Trait("Task11Cycle", "BHistory")]
    public async Task B16_Select_recent_session_builds_detail_from_session_and_current_correction()
    {
        var session = Session(
            sessionId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            gameId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            start: Utc(10),
            end: Utc(11));

        var fixture =
            CreateFixture([session]);

        fixture.CorrectionStore.Set(
            new SessionCorrectionPolicy()
                .Create(
                    session,
                    new SessionCorrectionRequest(
                        session.SessionId,
                        Utc(10, 15),
                        Utc(11, 45),
                        "Correction sélection"),
                    Utc(14)));

        await fixture.Sut.RefreshAsync(
            CancellationToken.None);

        await fixture.Sut.SelectRecentSessionAsync(
            session.SessionId,
            CancellationToken.None);

        var detail =
            Assert.IsType<SessionDetailViewModel>(
                fixture.Sut.SelectedSessionDetail);

        Assert.Equal(
            "Jeu local",
            detail.Title);

        Assert.Equal(
            Label(Utc(10)),
            detail.ObservedStartedAtLabel);

        Assert.Equal(
            "1:00:00",
            detail.ObservedDurationLabel);

        Assert.Equal(
            Label(Utc(10, 15)),
            detail.EffectiveStartedAtLabel);

        Assert.Equal(
            "1:30:00",
            detail.EffectiveDurationLabel);

        Assert.True(
            detail.IsCorrected);

        Assert.False(
            detail.IsRecovered);

        Assert.Contains(
            session.SessionId,
            fixture.SessionStore.GetCalls);
    }

    [Fact]
    [Trait("Task11Cycle", "BHistory")]
    public async Task B16_Saving_selected_correction_reloads_history_and_selected_detail_for_same_session()
    {
        var session = Session(
            sessionId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            gameId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            start: Utc(10),
            end: Utc(11));

        var fixture =
            CreateFixture([session]);

        await fixture.Sut.RefreshAsync(
            CancellationToken.None);

        await fixture.Sut.SelectRecentSessionAsync(
            session.SessionId,
            CancellationToken.None);

        var before =
            Assert.IsType<SessionDetailViewModel>(
                fixture.Sut.SelectedSessionDetail);

        Assert.False(before.IsCorrected);
        Assert.Equal(
            "1:00:00",
            before.EffectiveDurationLabel);

        before.Correction.CorrectedStartedAtText =
            "2026-09-12T10:15:00+00:00";

        before.Correction.CorrectedEndedAtText =
            "2026-09-12T11:45:00+00:00";

        before.Correction.Reason =
            "Correction depuis le détail";

        await before.Correction.SaveAsync(
            CancellationToken.None);

        var history =
            Assert.Single(
                fixture.Sut.RecentSessions);

        Assert.Equal(
            session.SessionId,
            history.SessionId);

        Assert.True(
            history.IsCorrected);

        Assert.Equal(
            "1:30:00",
            history.DurationLabel);

        var after =
            Assert.IsType<SessionDetailViewModel>(
                fixture.Sut.SelectedSessionDetail);

        Assert.Equal(
            session.SessionId,
            fixture.Runtime.LastRequest?.SessionId);

        Assert.NotSame(
            before,
            after);

        Assert.True(
            after.IsCorrected);

        Assert.Equal(
            Label(Utc(10, 15)),
            after.EffectiveStartedAtLabel);

        Assert.Equal(
            "1:30:00",
            after.EffectiveDurationLabel);

        Assert.Equal(
            session.SessionId,
            Assert.Single(
                fixture.Sut.RecentSessions)
                .SessionId);
    }

    private static Fixture CreateFixture(
        IReadOnlyList<GameSession> recent)
    {
        var sessionStore =
            new RecordingSessionStore(
                recent);

        var correctionStore =
            new RecordingCorrectionStore();

        var timeProvider =
            new FixedTimeProvider();

        var policy =
            new SessionCorrectionPolicy();

        var runtime =
            new PersistingRuntime(
                sessionStore,
                correctionStore,
                policy,
                timeProvider);

        var monitor =
            new SessionMonitor(
                runtime,
                SessionMonitorOptions.Default);

        var libraryStore =
            new EmptyLibraryStore();

        var sut =
            new SessionViewModel(
                libraryStore,
                monitor,
                timeProvider,
                sessionStore,
                correctionStore,
                runtime,
                policy);

        return new Fixture(
            sut,
            sessionStore,
            correctionStore,
            runtime);
    }

    private static GameSession Session(
        Guid sessionId,
        Guid gameId,
        DateTimeOffset start,
        DateTimeOffset end,
        SessionState state = SessionState.Ended,
        SessionEndReason endReason =
            SessionEndReason.ProcessExited) =>
        new(
            sessionId,
            gameId,
            start,
            end,
            end,
            state,
            endReason,
            SessionDetectionSource.ProcessMonitor,
            start,
            end);

    private static DateTimeOffset Utc(
        int hour,
        int minute = 0) =>
        new(
            2026,
            9,
            12,
            hour,
            minute,
            0,
            TimeSpan.Zero);

    private static string Label(
        DateTimeOffset timestamp) =>
        timestamp
            .ToLocalTime()
            .ToString(
                "g",
                CultureInfo.CurrentCulture);

    private sealed record Fixture(
        SessionViewModel Sut,
        RecordingSessionStore SessionStore,
        RecordingCorrectionStore CorrectionStore,
        PersistingRuntime Runtime);

    private sealed class RecordingSessionStore :
        ISessionStore
    {
        private readonly Dictionary<Guid, GameSession>
            _sessions;

        public RecordingSessionStore(
            IReadOnlyList<GameSession> recent)
        {
            Recent = recent;
            _sessions =
                recent.ToDictionary(
                    session => session.SessionId);
        }

        public IReadOnlyList<GameSession> Recent
        {
            get;
            set;
        }

        public int GetRecentCallCount
        {
            get;
            private set;
        }

        public List<Guid> GetCalls
        {
            get;
        } = [];

        public Task UpsertAsync(
            GameSession session,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            _sessions[session.SessionId] =
                session;

            return Task.CompletedTask;
        }

        public Task<GameSession?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            GetCalls.Add(
                sessionId);

            _sessions.TryGetValue(
                sessionId,
                out var session);

            return Task.FromResult(
                session);
        }

        public Task<IReadOnlyList<GameSession>>
            GetActiveAsync(
                CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult<
                IReadOnlyList<GameSession>>(
                    []);
        }

        public Task<IReadOnlyList<GameSession>>
            GetRecentAsync(
                int limit,
                CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            GetRecentCallCount++;

            return Task.FromResult(
                Recent);
        }
    }

    private sealed class RecordingCorrectionStore :
        ISessionCorrectionStore
    {
        private readonly Dictionary<Guid, SessionCorrection>
            _corrections = [];

        public List<Guid> GetCalls
        {
            get;
        } = [];

        public void Set(
            SessionCorrection correction) =>
            _corrections[
                correction.SessionId] =
                    correction;

        public Task UpsertAsync(
            SessionCorrection correction,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Set(correction);

            return Task.CompletedTask;
        }

        public Task<SessionCorrection?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            GetCalls.Add(
                sessionId);

            _corrections.TryGetValue(
                sessionId,
                out var correction);

            return Task.FromResult(
                correction);
        }
    }

    private sealed class PersistingRuntime :
        ISessionRuntime
    {
        private readonly RecordingSessionStore
            _sessionStore;

        private readonly RecordingCorrectionStore
            _correctionStore;

        private readonly SessionCorrectionPolicy
            _policy;

        private readonly TimeProvider
            _timeProvider;

        public PersistingRuntime(
            RecordingSessionStore sessionStore,
            RecordingCorrectionStore correctionStore,
            SessionCorrectionPolicy policy,
            TimeProvider timeProvider)
        {
            _sessionStore = sessionStore;
            _correctionStore = correctionStore;
            _policy = policy;
            _timeProvider = timeProvider;
        }

        public SessionCorrectionRequest?
            LastRequest
        {
            get;
            private set;
        }

        public Task<SessionRuntimeSnapshot>
            RefreshAsync(
                CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "History projection must not start the polling runtime.");

        public async Task CorrectSessionAsync(
            SessionCorrectionRequest correction,
            CancellationToken cancellationToken)
        {
            LastRequest =
                correction;

            var session =
                await _sessionStore.GetAsync(
                    correction.SessionId,
                    cancellationToken);

            Assert.NotNull(
                session);

            var persisted =
                _policy.Create(
                    session!,
                    correction,
                    _timeProvider.GetUtcNow());

            await _correctionStore.UpsertAsync(
                persisted,
                cancellationToken);
        }
    }

    private sealed class FixedTimeProvider :
        TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            Utc(14);
    }

    private sealed class EmptyLibraryStore :
        ILibraryStore
    {
        private readonly LibrarySnapshot
            _snapshot =
                CreateEmptyLibrarySnapshot();

        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _snapshot);
        }

        private static LibrarySnapshot
            CreateEmptyLibrarySnapshot()
        {
            foreach (var constructor in
                     typeof(LibrarySnapshot)
                         .GetConstructors()
                         .OrderBy(
                             constructor =>
                                 constructor
                                     .GetParameters()
                                     .Length))
            {
                var parameters =
                    constructor.GetParameters();

                var arguments =
                    parameters
                        .Select(
                            parameter =>
                                DefaultValue(
                                    parameter.ParameterType))
                        .ToArray();

                try
                {
                    var candidate =
                        (LibrarySnapshot)
                        constructor.Invoke(
                            arguments);

                    if (candidate.Games is not null)
                    {
                        return candidate;
                    }
                }
                catch (
                    TargetInvocationException)
                {
                    // Try the next public constructor.
                }
                catch (
                    ArgumentException)
                {
                    // Try the next public constructor.
                }
            }

            var uninitialized =
                (LibrarySnapshot)
                RuntimeHelpers
                    .GetUninitializedObject(
                        typeof(LibrarySnapshot));

            var gamesProperty =
                typeof(LibrarySnapshot)
                    .GetProperty(
                        nameof(
                            LibrarySnapshot
                                .Games));

            Assert.NotNull(
                gamesProperty);

            var emptyGames =
                DefaultValue(
                    gamesProperty!
                        .PropertyType);

            var backingField =
                typeof(LibrarySnapshot)
                    .GetField(
                        $"<{nameof(LibrarySnapshot.Games)}>k__BackingField",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);

            Assert.NotNull(
                backingField);

            backingField!.SetValue(
                uninitialized,
                emptyGames);

            Assert.NotNull(
                uninitialized.Games);

            return uninitialized;
        }

        private static object?
            DefaultValue(
                Type type)
        {
            if (type == typeof(string))
            {
                return string.Empty;
            }

            if (type.IsArray)
            {
                return Array.CreateInstance(
                    type.GetElementType()!,
                    0);
            }

            if (type.IsGenericType)
            {
                var definition =
                    type.GetGenericTypeDefinition();

                var genericArguments =
                    type.GetGenericArguments();

                if (definition ==
                        typeof(IReadOnlyList<>) ||
                    definition ==
                        typeof(IReadOnlyCollection<>) ||
                    definition ==
                        typeof(IEnumerable<>) ||
                    definition ==
                        typeof(ICollection<>) ||
                    definition ==
                        typeof(IList<>))
                {
                    return Array.CreateInstance(
                        genericArguments[0],
                        0);
                }

                if (definition ==
                    typeof(IReadOnlyDictionary<,>))
                {
                    var dictionaryType =
                        typeof(Dictionary<,>)
                            .MakeGenericType(
                                genericArguments);

                    return Activator.CreateInstance(
                        dictionaryType);
                }
            }

            if (type.IsValueType)
            {
                return Activator.CreateInstance(
                    type);
            }

            var parameterless =
                type.GetConstructor(
                    Type.EmptyTypes);

            if (parameterless is not null)
            {
                return parameterless.Invoke(
                    null);
            }

            return null;
        }
    }
}
