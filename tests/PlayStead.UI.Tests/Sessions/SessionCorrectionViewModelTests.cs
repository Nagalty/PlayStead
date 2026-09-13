using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using PlayStead.Core.Sessions;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class SessionCorrectionViewModelTests
{

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B1_Initial_inputs_without_correction_use_observed_times()
    {
        var sut = CreateEditor();

        AssertTimestamp(Utc(10), sut.CorrectedStartedAtText);
        AssertTimestamp(Utc(11), sut.CorrectedEndedAtText);
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B1_Initial_inputs_with_correction_use_effective_times()
    {
        var sut = CreateEditor(correction: ExistingCorrection(Utc(10, 15), Utc(11, 45)));

        AssertTimestamp(Utc(10, 15), sut.CorrectedStartedAtText);
        AssertTimestamp(Utc(11, 45), sut.CorrectedEndedAtText);
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B1_Existing_correction_reason_is_preserved()
    {
        var sut = CreateEditor(correction: ExistingCorrection(Utc(10, 15), Utc(11, 45)));

        Assert.Equal("Correction manuelle", sut.Reason);
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B1_Initial_valid_interval_can_be_saved_without_validation_error()
    {
        var sut = CreateEditor();

        Assert.True(sut.CanSave);
        Assert.True(string.IsNullOrWhiteSpace(sut.ValidationMessage));
    }

    [Theory]
    [InlineData("2026-09-12T09:59:00+00:00")]
    [InlineData("2026-09-12T10:00:00+00:00")]
    [Trait("Task11Cycle", "B")]
    public void B2_End_before_or_equal_to_start_disables_save_with_message(string end)
    {
        var sut = CreateEditor();
        sut.CorrectedStartedAtText = "2026-09-12T10:00:00+00:00";
        sut.CorrectedEndedAtText = end;

        Assert.False(sut.CanSave);
        Assert.False(string.IsNullOrWhiteSpace(sut.ValidationMessage));
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B2_Two_absent_overrides_disable_save_with_message()
    {
        var sut = CreateEditor();
        sut.CorrectedStartedAtText = string.Empty;
        sut.CorrectedEndedAtText = string.Empty;

        Assert.False(sut.CanSave);
        Assert.False(string.IsNullOrWhiteSpace(sut.ValidationMessage));
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B2_Active_session_cannot_be_corrected()
    {
        var active = Session() with { State = SessionState.Active, ObservedEndedAtUtc = null, EndReason = null };
        var sut = CreateEditor(session: active);
        SetValidInputs(sut);

        Assert.False(sut.CanSave);
        Assert.False(string.IsNullOrWhiteSpace(sut.ValidationMessage));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Task11Cycle", "B")]
    public void B2_Invalid_timestamp_text_disables_save_with_message(bool invalidStart)
    {
        var sut = CreateEditor();
        SetValidInputs(sut);
        if (invalidStart)
            sut.CorrectedStartedAtText = "not-a-timestamp";
        else
            sut.CorrectedEndedAtText = "not-a-timestamp";

        Assert.False(sut.CanSave);
        Assert.False(string.IsNullOrWhiteSpace(sut.ValidationMessage));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Task11Cycle", "B")]
    public void B3_Time_edit_notifies_input_validation_and_can_save(bool editStart)
    {
        var sut = CreateEditor();
        SetValidInputs(sut);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        if (editStart)
            sut.CorrectedStartedAtText = "2026-09-12T12:00:00+00:00";
        else
            sut.CorrectedEndedAtText = "2026-09-12T09:00:00+00:00";

        Assert.Contains(editStart ? nameof(sut.CorrectedStartedAtText) : nameof(sut.CorrectedEndedAtText), changed);
        Assert.Contains(nameof(sut.ValidationMessage), changed);
        Assert.Contains(nameof(sut.CanSave), changed);
        Assert.False(sut.CanSave);
        Assert.False(string.IsNullOrWhiteSpace(sut.ValidationMessage));
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B3_Reason_edit_notifies_reason()
    {
        var sut = CreateEditor();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        sut.Reason = "Nouvelle raison";

        Assert.Equal("Nouvelle raison", sut.Reason);
        Assert.Contains(nameof(sut.Reason), changed);
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B3_Repairing_invalid_end_enables_save_and_clears_message()
    {
        var sut = CreateEditor();
        sut.CorrectedStartedAtText = "2026-09-12T10:00:00+00:00";
        sut.CorrectedEndedAtText = "2026-09-12T09:00:00+00:00";
        sut.CorrectedEndedAtText = "2026-09-12T11:00:00+00:00";

        Assert.True(sut.CanSave);
        Assert.True(string.IsNullOrWhiteSpace(sut.ValidationMessage));
    }

    [Theory]
    [InlineData("2026-09-12T10:00:00+00:00", "2026-09-12T09:00:00+00:00")]
    [InlineData("2026-09-12T10:00:00+00:00", "2026-09-12T10:00:00+00:00")]
    [InlineData("", "")]
    [Trait("Task11Cycle", "B")]
    public async Task B4_Invalid_save_calls_neither_runtime_nor_refresh(string start, string end)
    {
        var runtime = new RecordingRuntime();
        var refreshCount = 0;
        var sut = CreateEditor(runtime, refresh: _ => { refreshCount++; return Task.CompletedTask; });
        sut.CorrectedStartedAtText = start;
        sut.CorrectedEndedAtText = end;

        await sut.SaveAsync(CancellationToken.None);

        Assert.Empty(runtime.Calls);
        Assert.Equal(0, refreshCount);
        Assert.False(sut.CanSave);
    }

    [Theory]
    [InlineData("Raison exacte")]
    [InlineData(null)]
    [Trait("Task11Cycle", "B")]
    public async Task B5_Valid_save_sends_one_exact_traceable_request(string? reason)
    {
        var runtime = new RecordingRuntime();
        var sut = CreateEditor(runtime);
        // Explicit offsets also exercise conversion to the UTC request boundary.
        sut.CorrectedStartedAtText = "2026-09-12T12:15:00+02:00";
        sut.CorrectedEndedAtText = "2026-09-12T13:45:00+02:00";
        sut.Reason = reason;
        using var cancellation = new CancellationTokenSource();

        await sut.SaveAsync(cancellation.Token);

        var call = Assert.Single(runtime.Calls);
        Assert.Equal(Session().SessionId, call.Request.SessionId);
        Assert.Equal(Utc(10, 15), call.Request.CorrectedStartedAtUtc);
        Assert.Equal(Utc(11, 45), call.Request.CorrectedEndedAtUtc);
        Assert.Equal(TimeSpan.Zero, call.Request.CorrectedStartedAtUtc!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, call.Request.CorrectedEndedAtUtc!.Value.Offset);
        Assert.Equal(reason, call.Request.Reason);
        Assert.Equal(cancellation.Token, call.Token);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Task11Cycle", "B")]
    public async Task B5_Partial_save_preserves_the_absent_override(bool startOnly)
    {
        var runtime = new RecordingRuntime();
        var sut = CreateEditor(runtime);
        sut.CorrectedStartedAtText = startOnly ? "2026-09-12T10:15:00+00:00" : string.Empty;
        sut.CorrectedEndedAtText = startOnly ? string.Empty : "2026-09-12T11:45:00+00:00";

        await sut.SaveAsync(CancellationToken.None);

        var call = Assert.Single(runtime.Calls);
        Assert.Equal(Session().SessionId, call.Request.SessionId);
        Assert.Equal(startOnly ? Utc(10, 15) : (DateTimeOffset?)null, call.Request.CorrectedStartedAtUtc);
        Assert.Equal(startOnly ? (DateTimeOffset?)null : Utc(11, 45), call.Request.CorrectedEndedAtUtc);
        Assert.Null(call.Request.Reason);
        Assert.Equal(CancellationToken.None, call.Token);
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public async Task B6_Refresh_starts_after_success_and_SaveAsync_awaits_its_completion()
    {
        var saveRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new RecordingRuntime { SaveResult = () => saveRelease.Task };
        var refreshCount = 0;
        var refreshToken = CancellationToken.None;
        var sut = CreateEditor(runtime, refresh: async token =>
        {
            refreshCount++;
            refreshToken = token;
            refreshStarted.TrySetResult();
            await refreshRelease.Task;
        });
        SetValidInputs(sut);
        using var cancellation = new CancellationTokenSource();
        var saving = sut.SaveAsync(cancellation.Token);

        try
        {
            // A timeout bounds a broken implementation; no sleep schedules the asserted order.
            await Task.WhenAny(runtime.Started.Task, saving).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(runtime.Started.Task.IsCompletedSuccessfully, "CorrectSessionAsync must start.");
            Assert.Single(runtime.Calls);
            Assert.False(refreshStarted.Task.IsCompleted, "Refresh must wait for successful correction.");
            Assert.False(saving.IsCompleted);

            saveRelease.TrySetResult();
            await Task.WhenAny(refreshStarted.Task, saving).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(refreshStarted.Task.IsCompletedSuccessfully, "Refresh must start after correction.");
            Assert.Equal(1, refreshCount);
            Assert.Equal(cancellation.Token, refreshToken);
            Assert.False(saving.IsCompleted, "SaveAsync must await refresh completion.");

            refreshRelease.TrySetResult();
            await saving.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Single(runtime.Calls);
            Assert.Equal(1, refreshCount);
        }
        finally
        {
            saveRelease.TrySetResult();
            refreshRelease.TrySetResult();
        }
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public async Task B7_Save_error_remains_observable_without_refresh_and_allows_retry()
    {
        var failure = new InvalidOperationException("Controlled save failure.");
        var runtime = new RecordingRuntime { SaveResult = () => Task.FromException(failure) };
        var refreshCount = 0;
        var sut = CreateEditor(runtime, refresh: _ => { refreshCount++; return Task.CompletedTask; });
        SetValidInputs(sut);

        var thrown = await Record.ExceptionAsync(() => sut.SaveAsync(CancellationToken.None));

        Assert.Single(runtime.Calls);
        Assert.Equal(0, refreshCount);
        // The Cycle A contract supports a faulted Task or an observable ValidationMessage.
        // Do not invent an Error property or prescribe localized error copy.
        Assert.True(ReferenceEquals(thrown, failure) || !string.IsNullOrWhiteSpace(sut.ValidationMessage),
            "The save failure must remain observable through the Task or ValidationMessage.");
        Assert.True(sut.CanSave, "A valid correction must be retryable after a failed save.");
    }

    private static DateTimeOffset Utc(int hour, int minute = 0) =>
        new(2026, 9, 12, hour, minute, 0, TimeSpan.Zero);

    private static GameSession Session() =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Utc(10), Utc(11), Utc(11), SessionState.Ended, SessionEndReason.ProcessExited,
            SessionDetectionSource.ProcessMonitor, Utc(10), Utc(11));

    private static SessionCorrection ExistingCorrection(DateTimeOffset? start, DateTimeOffset? end) =>
        new SessionCorrectionPolicy().Create(Session(),
            new SessionCorrectionRequest(Session().SessionId, start, end, "Correction manuelle"), Utc(12));

    private static SessionCorrectionViewModel CreateEditor(
        RecordingRuntime? runtime = null,
        SessionCorrection? correction = null,
        GameSession? session = null,
        Func<CancellationToken, Task>? refresh = null) =>
        new(session ?? Session(), correction, runtime ?? new RecordingRuntime(),
            new SessionCorrectionPolicy(), new FixedTimeProvider(),
            refresh ?? (_ => Task.CompletedTask));

    private static void SetValidInputs(SessionCorrectionViewModel sut)
    {
        sut.CorrectedStartedAtText = "2026-09-12T10:15:00+00:00";
        sut.CorrectedEndedAtText = "2026-09-12T11:45:00+00:00";
    }

    private static void AssertTimestamp(DateTimeOffset expected, string actual)
    {
        Assert.True(DateTimeOffset.TryParse(actual, System.Globalization.CultureInfo.CurrentCulture,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var parsed),
            $"Expected an editable timestamp for {expected:O}, got '{actual}'.");
        Assert.Equal(expected, parsed);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Utc(12);
    }

    private sealed class RecordingRuntime : ISessionRuntime
    {
        public List<(SessionCorrectionRequest Request, CancellationToken Token)> Calls { get; } = [];
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<Task> SaveResult { get; init; } = () => Task.CompletedTask;

        public Task CorrectSessionAsync(SessionCorrectionRequest correction, CancellationToken cancellationToken)
        {
            Calls.Add((correction, cancellationToken));
            Started.TrySetResult();
            return SaveResult();
        }

        public Task<SessionRuntimeSnapshot> RefreshAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The editor must use its UI refresh callback.");
    }
    [Fact]
    [Trait("Task11Cycle", "A")]
    public void Correction_contract_has_public_editable_inputs_and_read_only_validation()
    {
        var type = SessionCycleAContract.RequireType("SessionCorrectionViewModel");
        Assert.True(typeof(INotifyPropertyChanged).IsAssignableFrom(type));

        SessionCycleAContract.AssertProperty(type, "CorrectedStartedAtText", typeof(string), editable: true);
        SessionCycleAContract.AssertProperty(type, "CorrectedEndedAtText", typeof(string), editable: true);
        var reason = SessionCycleAContract.AssertProperty(type, "Reason", typeof(string), editable: true);
        var validation = SessionCycleAContract.AssertProperty(type, "ValidationMessage", typeof(string));
        SessionCycleAContract.AssertProperty(type, "CanSave", typeof(bool));

        var nullability = new NullabilityInfoContext();
        Assert.Equal(NullabilityState.Nullable, nullability.Create(reason).ReadState);
        Assert.Equal(NullabilityState.Nullable, nullability.Create(reason).WriteState);
        Assert.Equal(NullabilityState.Nullable, nullability.Create(validation).ReadState);
    }

    [Fact]
    [Trait("Task11Cycle", "A")]
    public void Correction_contract_has_Task_SaveAsync_with_one_required_CancellationToken()
    {
        var type = SessionCycleAContract.RequireType("SessionCorrectionViewModel");
        var method = type.GetMethod(
            "SaveAsync",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(CancellationToken)],
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
        Assert.False(method.IsGenericMethod);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
        Assert.False(parameter.IsOptional);
    }

    [Fact]
    [Trait("Task11Cycle", "A")]
    public void Correction_contract_is_constructible_with_existing_session_dependencies()
    {
        var type = SessionCycleAContract.RequireType("SessionCorrectionViewModel");
        var instance = SessionCycleAContract.CreateCorrection(type);
        Assert.IsAssignableFrom<INotifyPropertyChanged>(instance);
    }
}

// Shared only by the three Cycle A test files. No source-file inspection or B/C assertions.
internal static class SessionCycleAContract
{
    internal static Type RequireType(string name)
    {
        var fullName = $"PlayStead.UI.Sessions.{name}";
        var type = typeof(SessionViewModel).Assembly.GetType(fullName);
        Assert.True(type is not null, $"Cycle A requires the production type {fullName}.");
        Assert.True(type!.IsPublic, $"{fullName} must be public.");
        Assert.True(type.IsClass, $"{fullName} must be a class.");
        Assert.False(type.IsAbstract);
        Assert.False(type.ContainsGenericParameters);
        return type;
    }

    internal static PropertyInfo AssertProperty(
        Type owner,
        string name,
        Type propertyType,
        bool editable = false)
    {
        var property = owner.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(property is not null, $"{owner.Name}.{name} is required by Cycle A.");
        Assert.Equal(propertyType, property!.PropertyType);
        Assert.Empty(property.GetIndexParameters());
        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod.IsPublic);
        Assert.False(property.GetMethod.IsStatic);

        if (editable)
        {
            Assert.NotNull(property.SetMethod);
            Assert.True(property.SetMethod.IsPublic);
            Assert.False(property.SetMethod.IsStatic);
            Assert.DoesNotContain(
                typeof(IsExternalInit),
                property.SetMethod.ReturnParameter.GetRequiredCustomModifiers());
        }
        else
        {
            Assert.False(property.SetMethod?.IsPublic == true,
                $"{owner.Name}.{name} must not have a public setter.");
        }

        return property;
    }

    internal static object CreateCorrection(Type type)
    {
        var constructor = type.GetConstructor(
            [
                typeof(GameSession),
                typeof(SessionCorrection),
                typeof(ISessionRuntime),
                typeof(SessionCorrectionPolicy),
                typeof(TimeProvider),
                typeof(Func<CancellationToken, Task>)
            ]);
        Assert.True(constructor is not null,
            "Expected public constructor (GameSession, SessionCorrection?, ISessionRuntime, " +
            "SessionCorrectionPolicy, TimeProvider, Func<CancellationToken, Task>).");
        Assert.Equal(
            new[] { typeof(GameSession), typeof(SessionCorrection), typeof(ISessionRuntime),
                typeof(SessionCorrectionPolicy), typeof(TimeProvider), typeof(Func<CancellationToken, Task>) },
            constructor!.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.Equal(NullabilityState.Nullable,
            new NullabilityInfoContext().Create(constructor.GetParameters()[1]).ReadState);

        return constructor.Invoke(
            [
                CreateSession(),
                null,
                new UnusedSessionRuntime(),
                new SessionCorrectionPolicy(),
                TimeProvider.System,
                (Func<CancellationToken, Task>)(_ => Task.CompletedTask)
            ]);
    }

    internal static GameSession CreateSession()
    {
        var start = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);
        return new GameSession(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            start, end, end, SessionState.Ended, SessionEndReason.ProcessExited,
            default, start, end);
    }

    // Construction needs a runtime reference, never an executing process monitor or database.
    private sealed class UnusedSessionRuntime : ISessionRuntime
    {
        public Task<SessionRuntimeSnapshot> RefreshAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Cycle A construction must not run the session runtime.");

        public Task CorrectSessionAsync(
            SessionCorrectionRequest correction,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Cycle A construction must not save a correction.");
    }
}
