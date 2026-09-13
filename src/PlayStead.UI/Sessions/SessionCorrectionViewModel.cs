using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using PlayStead.Core.Sessions;

namespace PlayStead.UI.Sessions;

public sealed class SessionCorrectionViewModel : INotifyPropertyChanged
{
    private readonly GameSession _session;
    private readonly ISessionRuntime _runtime;
    private readonly SessionCorrectionPolicy _policy;
    private readonly TimeProvider _timeProvider;
    private readonly Func<CancellationToken, Task> _refreshAsync;

    private string _correctedStartedAtText;
    private string _correctedEndedAtText;
    private string? _reason;

    public SessionCorrectionViewModel(
        GameSession session,
        SessionCorrection? correction,
        ISessionRuntime runtime,
        SessionCorrectionPolicy policy,
        TimeProvider timeProvider,
        Func<CancellationToken, Task> refreshAsync)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(refreshAsync);

        _session = session;
        _runtime = runtime;
        _policy = policy;
        _timeProvider = timeProvider;
        _refreshAsync = refreshAsync;

        var effective = _policy.Resolve(session, correction);

        _correctedStartedAtText =
            ToEditableTimestamp(effective.StartedAtUtc);

        _correctedEndedAtText =
            ToEditableTimestamp(effective.EndedAtUtc);

        _reason = correction?.Reason;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CorrectedStartedAtText
    {
        get => _correctedStartedAtText;
        set
        {
            if (string.Equals(
                    _correctedStartedAtText,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _correctedStartedAtText = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(ValidationMessage));
            OnPropertyChanged(nameof(CanSave));
        }
    }

    public string CorrectedEndedAtText
    {
        get => _correctedEndedAtText;
        set
        {
            if (string.Equals(
                    _correctedEndedAtText,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _correctedEndedAtText = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(ValidationMessage));
            OnPropertyChanged(nameof(CanSave));
        }
    }

    public string? Reason
    {
        get => _reason;
        set
        {
            if (string.Equals(
                    _reason,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _reason = value;
            OnPropertyChanged();
        }
    }

    public string? ValidationMessage
    {
        get
        {
            _ = TryCreateRequest(
                out _,
                out var validationMessage);

            return validationMessage;
        }
    }

    public bool CanSave =>
        TryCreateRequest(
            out _,
            out _);

    public async Task SaveAsync(
        CancellationToken cancellationToken)
    {
        if (!TryCreateRequest(
                out var request,
                out _))
        {
            return;
        }

        await _runtime.CorrectSessionAsync(
            request!,
            cancellationToken);

        await _refreshAsync(
            cancellationToken);
    }

    private bool TryCreateRequest(
        out SessionCorrectionRequest? request,
        out string? validationMessage)
    {
        request = null;

        if (!TryParseOptionalTimestamp(
                CorrectedStartedAtText,
                "Le début corrigé n'est pas un horodatage valide.",
                out var correctedStartedAtUtc,
                out validationMessage))
        {
            return false;
        }

        if (!TryParseOptionalTimestamp(
                CorrectedEndedAtText,
                "La fin corrigée n'est pas un horodatage valide.",
                out var correctedEndedAtUtc,
                out validationMessage))
        {
            return false;
        }

        var candidate =
            new SessionCorrectionRequest(
                _session.SessionId,
                correctedStartedAtUtc,
                correctedEndedAtUtc,
                Reason);

        try
        {
            _policy.Create(
                _session,
                candidate,
                _timeProvider.GetUtcNow());

            request = candidate;
            validationMessage = null;
            return true;
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or InvalidOperationException)
        {
            validationMessage = exception.Message;
            return false;
        }
    }

    private static bool TryParseOptionalTimestamp(
        string text,
        string invalidMessage,
        out DateTimeOffset? timestampUtc,
        out string? validationMessage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            timestampUtc = null;
            validationMessage = null;
            return true;
        }

        if (!DateTimeOffset.TryParse(
                text,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            timestampUtc = null;
            validationMessage = invalidMessage;
            return false;
        }

        timestampUtc =
            parsed.ToUniversalTime();

        validationMessage = null;
        return true;
    }

    private static string ToEditableTimestamp(
        DateTimeOffset? timestamp) =>
        timestamp?.ToString(
            "O",
            CultureInfo.InvariantCulture)
        ?? string.Empty;

    private void OnPropertyChanged(
        [CallerMemberName]
        string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
}
