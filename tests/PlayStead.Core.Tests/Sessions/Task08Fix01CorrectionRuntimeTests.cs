using System.Reflection;
using System.Runtime.ExceptionServices;
using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.Sessions;

public sealed class Task08Fix01CorrectionRuntimeTests
{
    private static readonly Guid GameId =
        Guid.Parse("83838383-8383-4383-8383-838383838383");

    private static readonly Guid SessionId =
        Guid.Parse("84848484-8484-4484-8484-848484848484");

    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CorrectionTime =
        new(2026, 9, 13, 10, 15, 0, TimeSpan.Zero);

    [Fact]
    public void Runtime_contract_exposes_CorrectSessionAsync_with_request_and_cancellation()
    {
        var method = RequireCorrectSessionMethod();
        var parameters = method.GetParameters();

        Assert.Equal(typeof(Task), method.ReturnType);
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(SessionCorrectionRequest), parameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }

    [Fact]
    public async Task Completed_session_correction_is_persisted_separately_without_rewriting_observation()
    {
        var method = RequireCorrectSessionMethod();
        var observed = EndedSession();
        var sessions = new FakeSessionStore(observed);
        var corrections = new FakeSessionCorrectionStore();
        var runtime = CreateRuntime(sessions, corrections);

        var request = CreateRequest(
            SessionId,
            T0.AddMinutes(5),
            T0.AddMinutes(65),
            "Correction après vérification");

        await InvokeCorrectAsync(method, runtime, request);

        Assert.Empty(sessions.Upserts);

        var persisted = Assert.Single(corrections.Upserts);
        Assert.Equal(SessionId, Read<Guid>(persisted, "SessionId"));
        Assert.Equal(
            T0.AddMinutes(5),
            Read<DateTimeOffset?>(persisted, "CorrectedStartedAtUtc"));
        Assert.Equal(
            T0.AddMinutes(65),
            Read<DateTimeOffset?>(persisted, "CorrectedEndedAtUtc"));
        Assert.Equal(
            "Correction après vérification",
            Read<string?>(persisted, "Reason"));
        Assert.NotEqual(Guid.Empty, Read<Guid>(persisted, "CorrectionId"));
        Assert.Equal(
            CorrectionTime,
            Read<DateTimeOffset>(persisted, "CreatedAtUtc"));

        Assert.Equal(
            observed,
            await sessions.GetAsync(SessionId, CancellationToken.None));
    }

    [Fact]
    public async Task Active_session_correction_is_rejected()
    {
        var method = RequireCorrectSessionMethod();
        var runtime = CreateRuntime(
            new FakeSessionStore(ActiveSession()),
            new FakeSessionCorrectionStore());

        var request = CreateRequest(
            SessionId,
            T0,
            T0.AddMinutes(10),
            reason: null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => InvokeCorrectAsync(method, runtime, request));
    }

    [Fact]
    public async Task Correction_with_effective_end_before_effective_start_is_rejected()
    {
        var method = RequireCorrectSessionMethod();
        var runtime = CreateRuntime(
            new FakeSessionStore(EndedSession()),
            new FakeSessionCorrectionStore());

        var request = CreateRequest(
            SessionId,
            T0.AddMinutes(70),
            T0.AddMinutes(65),
            "Invalid interval");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => InvokeCorrectAsync(method, runtime, request));
    }

    private static MethodInfo RequireCorrectSessionMethod()
    {
        var method = typeof(ISessionRuntime).GetMethod(
            "CorrectSessionAsync",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        return method!;
    }

    private static SessionRuntime CreateRuntime(
        ISessionStore sessions,
        ISessionCorrectionStore corrections)
    {
        var constructor = typeof(SessionRuntime)
            .GetConstructors()
            .OrderByDescending(x => x.GetParameters().Length)
            .FirstOrDefault();

        Assert.NotNull(constructor);

        var arguments = constructor!
            .GetParameters()
            .Select(parameter => CreateRuntimeDependency(
                parameter.ParameterType,
                sessions,
                corrections))
            .ToArray();

        return Assert.IsType<SessionRuntime>(constructor.Invoke(arguments));
    }

    private static object CreateRuntimeDependency(
        Type dependencyType,
        ISessionStore sessions,
        ISessionCorrectionStore corrections)
    {
        if (dependencyType == typeof(IProcessSnapshotSource))
            return new EmptyProcessSnapshotSource();

        if (dependencyType == typeof(IProcessSignatureStore))
            return new EmptyProcessSignatureStore();

        if (dependencyType == typeof(ISessionStore))
            return sessions;

        if (dependencyType == typeof(ProcessSignatureMatcher))
            return new ProcessSignatureMatcher();

        if (dependencyType == typeof(SessionTransitionPolicy))
            return new SessionTransitionPolicy();

        if (dependencyType == typeof(ISessionCorrectionStore))
            return corrections;

        if (dependencyType == typeof(SessionCorrectionPolicy))
            return new SessionCorrectionPolicy();

        if (dependencyType == typeof(TimeProvider))
            return new FixedTimeProvider(CorrectionTime);

        throw new Xunit.Sdk.XunitException(
            $"Unsupported SessionRuntime constructor dependency in Task 8 FIX01 RED: {dependencyType.FullName}");
    }

    private static SessionCorrectionRequest CreateRequest(
        Guid sessionId,
        DateTimeOffset? correctedStartedAtUtc,
        DateTimeOffset? correctedEndedAtUtc,
        string? reason)
    {
        var values = new Dictionary<string, object?>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["sessionId"] = sessionId,
            ["correctedStartedAtUtc"] = correctedStartedAtUtc,
            ["correctedEndedAtUtc"] = correctedEndedAtUtc,
            ["reason"] = reason
        };

        var constructor = typeof(SessionCorrectionRequest)
            .GetConstructors()
            .OrderByDescending(x => x.GetParameters().Length)
            .FirstOrDefault();

        Assert.NotNull(constructor);

        var parameters = constructor!.GetParameters();

        Assert.Contains(
            parameters,
            x => string.Equals(
                x.Name,
                "sessionId",
                StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            parameters,
            x => string.Equals(
                x.Name,
                "reason",
                StringComparison.OrdinalIgnoreCase));

        var arguments = parameters
            .Select(parameter =>
            {
                if (parameter.Name is not null &&
                    values.TryGetValue(parameter.Name, out var value))
                {
                    return value;
                }

                if (parameter.HasDefaultValue)
                {
                    return parameter.DefaultValue;
                }

                throw new Xunit.Sdk.XunitException(
                    $"No test value was supplied for SessionCorrectionRequest parameter '{parameter.Name}'.");
            })
            .ToArray();

        return Assert.IsType<SessionCorrectionRequest>(
            constructor.Invoke(arguments));
    }

    private static async Task InvokeCorrectAsync(
        MethodInfo method,
        ISessionRuntime runtime,
        SessionCorrectionRequest request)
    {
        try
        {
            var result = method.Invoke(
                runtime,
                new object?[] { request, CancellationToken.None });

            await Assert.IsAssignableFrom<Task>(result);
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static T Read<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);

        var value = property!.GetValue(target);

        if (value is null)
            return default!;

        if (typeof(T) == typeof(DateTimeOffset?) &&
            value is DateTimeOffset dateTimeOffset)
        {
            return (T)(object)(DateTimeOffset?)dateTimeOffset;
        }

        return Assert.IsType<T>(value);
    }

    private static GameSession EndedSession()
        => new(
            SessionId,
            GameId,
            T0,
            T0.AddMinutes(60),
            T0.AddMinutes(60),
            SessionState.Ended,
            SessionEndReason.ProcessExited,
            SessionDetectionSource.ProcessMonitor,
            T0,
            T0.AddMinutes(60));

    private static GameSession ActiveSession()
        => new(
            SessionId,
            GameId,
            T0,
            T0.AddMinutes(10),
            null,
            SessionState.Active,
            null,
            SessionDetectionSource.ProcessMonitor,
            T0,
            T0.AddMinutes(10));

    private sealed class EmptyProcessSnapshotSource : IProcessSnapshotSource
    {
        public Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ProcessSnapshot>>(
                Array.Empty<ProcessSnapshot>());
        }
    }

    private sealed class EmptyProcessSignatureStore : IProcessSignatureStore
    {
        public Task UpsertAsync(
            ProcessSignature signature,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<ProcessSignature?> GetAsync(
            Guid gameId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ProcessSignature?>(null);
        }

        public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ProcessSignature>>(
                Array.Empty<ProcessSignature>());
        }
    }

    private sealed class FakeSessionStore : ISessionStore
    {
        private readonly Dictionary<Guid, GameSession> _sessions;

        public FakeSessionStore(params GameSession[] sessions)
        {
            _sessions = sessions.ToDictionary(x => x.SessionId);
        }

        public List<GameSession> Upserts { get; } = [];

        public Task UpsertAsync(
            GameSession session,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Upserts.Add(session);
            _sessions[session.SessionId] = session;
            return Task.CompletedTask;
        }

        public Task<GameSession?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _sessions.TryGetValue(sessionId, out var session);
            return Task.FromResult(session);
        }

        public Task<IReadOnlyList<GameSession>> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GameSession>>(
                _sessions.Values
                    .Where(x => x.State == SessionState.Active)
                    .ToArray());
        }

        public Task<IReadOnlyList<GameSession>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GameSession>>(
                _sessions.Values
                    .OrderByDescending(x => x.ObservedStartedAtUtc)
                    .Take(limit)
                    .ToArray());
        }
    }

    private sealed class FakeSessionCorrectionStore : ISessionCorrectionStore
    {
        private readonly Dictionary<Guid, SessionCorrection> _corrections = new();

        public List<SessionCorrection> Upserts { get; } = [];

        public Task UpsertAsync(
            SessionCorrection correction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Upserts.Add(correction);
            _corrections[correction.SessionId] = correction;
            return Task.CompletedTask;
        }

        public Task<SessionCorrection?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _corrections.TryGetValue(sessionId, out var correction);
            return Task.FromResult(correction);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
