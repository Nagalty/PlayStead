using System.ComponentModel;
using System.Globalization;
using PlayStead.Core.Sessions;

namespace PlayStead.UI.Sessions;

public sealed class SessionDetailViewModel : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _propertyChanged;

    public SessionDetailViewModel(
        string title,
        GameSession session,
        SessionCorrection? correction,
        SessionCorrectionPolicy policy,
        SessionCorrectionViewModel correctionViewModel)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(correctionViewModel);

        var effective =
            policy.Resolve(
                session,
                correction);

        Title = title;

        ObservedStartedAtLabel =
            FormatTimestamp(
                session.ObservedStartedAtUtc);

        ObservedEndedAtLabel =
            FormatTimestamp(
                session.ObservedEndedAtUtc);

        ObservedDurationLabel =
            FormatDuration(
                session.ObservedStartedAtUtc,
                session.ObservedEndedAtUtc);

        EffectiveStartedAtLabel =
            FormatTimestamp(
                effective.StartedAtUtc);

        EffectiveEndedAtLabel =
            FormatTimestamp(
                effective.EndedAtUtc);

        EffectiveDurationLabel =
            FormatDuration(
                effective.StartedAtUtc,
                effective.EndedAtUtc);

        EndReasonLabel =
            session.EndReason?.ToString()
            ?? string.Empty;

        IsRecovered =
            session.State == SessionState.Recovered;

        IsCorrected =
            effective.IsManuallyCorrected;

        Correction =
            correctionViewModel;
    }

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }

    public string Title { get; }

    public string ObservedStartedAtLabel { get; }

    public string ObservedEndedAtLabel { get; }

    public string ObservedDurationLabel { get; }

    public string EffectiveStartedAtLabel { get; }

    public string EffectiveEndedAtLabel { get; }

    public string EffectiveDurationLabel { get; }

    public string EndReasonLabel { get; }

    public bool IsRecovered { get; }

    public bool IsCorrected { get; }

    public SessionCorrectionViewModel Correction { get; }

    private static string FormatTimestamp(
        DateTimeOffset timestamp) =>
        timestamp
            .ToLocalTime()
            .ToString(
                "g",
                CultureInfo.CurrentCulture);

    private static string FormatTimestamp(
        DateTimeOffset? timestamp) =>
        timestamp is null
            ? string.Empty
            : FormatTimestamp(
                timestamp.Value);

    private static string FormatDuration(
        DateTimeOffset startedAtUtc,
        DateTimeOffset? endedAtUtc)
    {
        if (endedAtUtc is null)
        {
            return string.Empty;
        }

        var elapsed =
            endedAtUtc.Value -
            startedAtUtc;

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
}
