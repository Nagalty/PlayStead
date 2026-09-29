using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class SessionWpfIntegrationTests
{
    [Fact]
    [Trait("Task11Cycle", "C1")]
    public void C1_recent_history_host_displays_items_from_recent_sessions_binding()
    {
        RunSta(() =>
        {
            var view =
                new SessionsView
                {
                    DataContext =
                        new FakeSessionsState(
                            hasRecentSessions: true,
                            recentSessions:
                            [
                                new FakeRecentSession(
                                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                                    "Escape from Testov",
                                    "12/09/2026 10:00",
                                    "1:30:00",
                                    IsRecovered: false,
                                    IsCorrected: true)
                            ])
                };

            FlushBindings(view);

            var host =
                Assert.IsType<Border>(
                    view.FindName("RecentHistoryHost"));

            var list =
                FindLogicalDescendant<ItemsControl>(
                    host);

            Assert.NotNull(list);
            Assert.Single(list!.Items);

            var item =
                Assert.IsType<FakeRecentSession>(
                    list.Items[0]);

            Assert.Equal(
                "Escape from Testov",
                item.Title);

            Assert.Equal(
                "1:30:00",
                item.DurationLabel);
        });
    }

    [Fact]
    [Trait("Task11Cycle", "C1")]
    public void C1_recent_history_host_displays_dedicated_empty_state_when_history_is_empty()
    {
        RunSta(() =>
        {
            var view =
                new SessionsView
                {
                    DataContext =
                        new FakeSessionsState(
                            hasRecentSessions: false,
                            recentSessions: [])
                };

            FlushBindings(view);

            var host =
                Assert.IsType<Border>(
                    view.FindName("RecentHistoryHost"));

            var emptyMessage =
                FindLogicalDescendants<TextBlock>(
                    host)
                    .SingleOrDefault(
                        text =>
                            string.Equals(
                                text.Text,
                                "Pas de session récente à te montrer.",
                                StringComparison.Ordinal));

            Assert.NotNull(emptyMessage);
            Assert.Equal(
                Visibility.Visible,
                emptyMessage!.Visibility);

            var list =
                FindLogicalDescendant<ItemsControl>(
                    host);

            Assert.True(
                list is null ||
                list.Visibility ==
                    Visibility.Collapsed);
        });
    }

    [Fact]
    [Trait("Task11Cycle", "C1")]
    public void C1_detail_view_binds_observed_and_effective_projection()
    {
        RunSta(() =>
        {
            var view =
                new SessionDetailView
                {
                    DataContext =
                        new FakeDetailState(
                            Title: "Escape from Testov",
                            ObservedStartedAtLabel: "12/09/2026 10:00",
                            ObservedEndedAtLabel: "12/09/2026 11:00",
                            ObservedDurationLabel: "1:00:00",
                            EffectiveStartedAtLabel: "12/09/2026 10:15",
                            EffectiveEndedAtLabel: "12/09/2026 11:45",
                            EffectiveDurationLabel: "1:30:00",
                            EndReasonLabel: "ProcessExited",
                            IsRecovered: false,
                            IsCorrected: true,
                            Correction:
                                new FakeCorrectionState(
                                    canSave: false,
                                    validationMessage:
                                        "Validation de test"))
                };

            FlushBindings(view);

            Assert.Equal(
                "Escape from Testov",
                Text(view, "DetailTitle"));

            Assert.Equal(
                "12/09/2026 10:00",
                Text(view, "ObservedStartText"));

            Assert.Equal(
                "12/09/2026 11:00",
                Text(view, "ObservedEndText"));

            Assert.Equal(
                "1:00:00",
                Text(view, "ObservedDurationText"));

            Assert.Equal(
                "12/09/2026 10:15",
                Text(view, "EffectiveStartText"));

            Assert.Equal(
                "12/09/2026 11:45",
                Text(view, "EffectiveEndText"));

            Assert.Equal(
                "1:30:00",
                Text(view, "EffectiveDurationText"));

            Assert.Equal(
                "ProcessExited",
                Text(view, "EndReasonText"));
        });
    }

    [Fact]
    [Trait("Task11Cycle", "C1")]
    public void C1_detail_badges_are_hidden_when_recovery_and_correction_states_are_false()
    {
        RunSta(() =>
        {
            var view =
                new SessionDetailView
                {
                    DataContext =
                        new FakeDetailState(
                            Title: "Test",
                            ObservedStartedAtLabel: "start",
                            ObservedEndedAtLabel: "end",
                            ObservedDurationLabel: "duration",
                            EffectiveStartedAtLabel: "start",
                            EffectiveEndedAtLabel: "end",
                            EffectiveDurationLabel: "duration",
                            EndReasonLabel: "ProcessExited",
                            IsRecovered: false,
                            IsCorrected: false,
                            Correction:
                                new FakeCorrectionState(
                                    canSave: false,
                                    validationMessage: null))
                };

            FlushBindings(view);

            var recoveryBadge =
                Assert.IsType<Border>(
                    view.FindName("RecoveryBadge"));

            var correctionBadge =
                Assert.IsType<Border>(
                    view.FindName("CorrectionBadge"));

            Assert.Equal(
                Visibility.Collapsed,
                recoveryBadge.Visibility);

            Assert.Equal(
                Visibility.Collapsed,
                correctionBadge.Visibility);
        });
    }

    [Fact]
    [Trait("Task11Cycle", "C1")]
    public void C1_correction_validation_and_save_enabled_state_follow_correction_view_model()
    {
        RunSta(() =>
        {
            var correction =
                new FakeCorrectionState(
                    canSave: false,
                    validationMessage:
                        "La correction est invalide.");

            var view =
                new SessionDetailView
                {
                    DataContext =
                        new FakeDetailState(
                            Title: "Test",
                            ObservedStartedAtLabel: "start",
                            ObservedEndedAtLabel: "end",
                            ObservedDurationLabel: "duration",
                            EffectiveStartedAtLabel: "start",
                            EffectiveEndedAtLabel: "end",
                            EffectiveDurationLabel: "duration",
                            EndReasonLabel: "ProcessExited",
                            IsRecovered: false,
                            IsCorrected: false,
                            Correction: correction)
                };

            FlushBindings(view);

            var validation =
                Assert.IsType<TextBlock>(
                    view.FindName(
                        "CorrectionValidationText"));

            var saveButton =
                Assert.IsType<Button>(
                    view.FindName(
                        "SaveCorrectionButton"));

            Assert.Equal(
                "La correction est invalide.",
                validation.Text);

            Assert.False(
                saveButton.IsEnabled);

            correction.CanSave = true;
            correction.ValidationMessage = null;

            FlushBindings(view);

            Assert.True(
                saveButton.IsEnabled);

            Assert.True(
                string.IsNullOrEmpty(
                    validation.Text));
        });
    }

    private static string Text(
        FrameworkElement view,
        string name) =>
        Assert.IsType<TextBlock>(
            view.FindName(name))
            .Text;

    private static T? FindLogicalDescendant<T>(
        DependencyObject root)
        where T : DependencyObject =>
        FindLogicalDescendants<T>(
                root)
            .FirstOrDefault();

    private static IEnumerable<T>
        FindLogicalDescendants<T>(
            DependencyObject root)
        where T : DependencyObject
    {
        foreach (var child in
                 LogicalTreeHelper.GetChildren(
                     root))
        {
            if (child is T match)
            {
                yield return match;
            }

            if (child is DependencyObject
                dependencyObject)
            {
                foreach (var nested in
                         FindLogicalDescendants<T>(
                             dependencyObject))
                {
                    yield return nested;
                }
            }
        }
    }

    private static void FlushBindings(
        FrameworkElement element)
    {
        element.ApplyTemplate();
        element.UpdateLayout();

        Dispatcher.CurrentDispatcher.Invoke(
            () => { },
            DispatcherPriority.DataBind);

        element.UpdateLayout();
    }

    private static void RunSta(
        Action action)
    {
        Exception? failure = null;

        var thread =
            new Thread(
                () =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                });

        thread.SetApartmentState(
            ApartmentState.STA);

        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo
                .Capture(failure)
                .Throw();
        }
    }

    private sealed record FakeRecentSession(
        Guid SessionId,
        string Title,
        string StartedAtLabel,
        string DurationLabel,
        bool IsRecovered,
        bool IsCorrected);

    private sealed class FakeSessionsState
    {
        public FakeSessionsState(
            bool hasRecentSessions,
            IReadOnlyList<FakeRecentSession>
                recentSessions)
        {
            HasRecentSessions =
                hasRecentSessions;

            RecentSessions =
                recentSessions;
        }

        public bool HasRecentSessions
        {
            get;
        }

        public IReadOnlyList<FakeRecentSession>
            RecentSessions
        {
            get;
        }

        public object? SelectedSessionDetail =>
            null;
    }

    private sealed record FakeDetailState(
        string Title,
        string ObservedStartedAtLabel,
        string ObservedEndedAtLabel,
        string ObservedDurationLabel,
        string EffectiveStartedAtLabel,
        string EffectiveEndedAtLabel,
        string EffectiveDurationLabel,
        string EndReasonLabel,
        bool IsRecovered,
        bool IsCorrected,
        FakeCorrectionState Correction);

    private sealed class FakeCorrectionState :
        INotifyPropertyChanged
    {
        private bool _canSave;
        private string? _validationMessage;

        public FakeCorrectionState(
            bool canSave,
            string? validationMessage)
        {
            _canSave = canSave;
            _validationMessage =
                validationMessage;
        }

        public event PropertyChangedEventHandler?
            PropertyChanged;

        public bool CanSave
        {
            get => _canSave;
            set
            {
                if (_canSave == value)
                {
                    return;
                }

                _canSave = value;
                OnPropertyChanged();
            }
        }

        public string? ValidationMessage
        {
            get => _validationMessage;
            set
            {
                if (string.Equals(
                        _validationMessage,
                        value,
                        StringComparison.Ordinal))
                {
                    return;
                }

                _validationMessage = value;
                OnPropertyChanged();
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
}
