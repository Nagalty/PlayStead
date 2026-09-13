using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using PlayStead.UI.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class SessionWpfInteractionTests
{
    [Fact]
    [Trait("Task11Cycle", "C2")]
    public void C2_recent_history_exposes_a_view_details_action()
    {
        var xaml =
            ReadRepositoryFile(
                "src",
                "PlayStead.UI",
                "Sessions",
                "SessionsView.xaml");

        Assert.Contains(
            "Voir détails",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "RecentSessionDetailsButton",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "RecentSessionDetailsButton_OnClick",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Task11Cycle", "C2")]
    public void C2_sessions_view_hosts_selected_session_detail()
    {
        RunSta(() =>
        {
            var selected =
                new object();

            var view =
                new SessionsView
                {
                    DataContext =
                        new FakeSessionsState(
                            selected)
                };

            FlushBindings(view);

            var host =
                Assert.IsType<ContentControl>(
                    view.FindName(
                        "SessionDetailHost"));

            Assert.Same(
                selected,
                host.Content);

            Assert.NotNull(
                host.ContentTemplate);

            Assert.IsType<SessionDetailView>(
                host.ContentTemplate!
                    .LoadContent());
        });
    }

    [Fact]
    [Trait("Task11Cycle", "C2")]
    public void C2_correct_session_action_opens_the_correction_panel()
    {
        RunSta(() =>
        {
            var view =
                new SessionDetailView
                {
                    DataContext =
                        FakeDetailState.Create()
                };

            FlushBindings(view);

            var panel =
                Assert.IsType<StackPanel>(
                    view.FindName(
                        "CorrectionPanel"));

            var button =
                Assert.IsType<Button>(
                    view.FindName(
                        "CorrectSessionButton"));

            Assert.Equal(
                Visibility.Collapsed,
                panel.Visibility);

            button.RaiseEvent(
                new RoutedEventArgs(
                    Button.ClickEvent));

            FlushBindings(view);

            Assert.Equal(
                Visibility.Visible,
                panel.Visibility);
        });
    }

    [Fact]
    [Trait("Task11Cycle", "C2")]
    public void C2_correction_editors_are_two_way_bound_to_correction_state()
    {
        RunSta(() =>
        {
            var correction =
                new FakeCorrectionState
                {
                    CorrectedStartedAtText =
                        "initial-start",
                    CorrectedEndedAtText =
                        "initial-end",
                    Reason =
                        "initial-reason"
                };

            var view =
                new SessionDetailView
                {
                    DataContext =
                        FakeDetailState.Create(
                            correction)
                };

            FlushBindings(view);

            var start =
                Assert.IsType<TextBox>(
                    view.FindName(
                        "CorrectionStartTextBox"));

            var end =
                Assert.IsType<TextBox>(
                    view.FindName(
                        "CorrectionEndTextBox"));

            var reason =
                Assert.IsType<TextBox>(
                    view.FindName(
                        "CorrectionReasonTextBox"));

            Assert.Equal(
                "initial-start",
                start.Text);

            Assert.Equal(
                "initial-end",
                end.Text);

            Assert.Equal(
                "initial-reason",
                reason.Text);

            start.Text =
                "edited-start";

            end.Text =
                "edited-end";

            reason.Text =
                "edited-reason";

            var startBinding =
                BindingOperations
                    .GetBindingExpression(
                        start,
                        TextBox.TextProperty);

            var endBinding =
                BindingOperations
                    .GetBindingExpression(
                        end,
                        TextBox.TextProperty);

            var reasonBinding =
                BindingOperations
                    .GetBindingExpression(
                        reason,
                        TextBox.TextProperty);

            Assert.NotNull(
                startBinding);

            Assert.NotNull(
                endBinding);

            Assert.NotNull(
                reasonBinding);

            Assert.Equal(
                BindingMode.TwoWay,
                startBinding!
                    .ParentBinding
                    .Mode);

            Assert.Equal(
                BindingMode.TwoWay,
                endBinding!
                    .ParentBinding
                    .Mode);

            Assert.Equal(
                BindingMode.TwoWay,
                reasonBinding!
                    .ParentBinding
                    .Mode);

            startBinding.UpdateSource();
            endBinding.UpdateSource();
            reasonBinding.UpdateSource();

            Assert.Equal(
                "edited-start",
                correction
                    .CorrectedStartedAtText);

            Assert.Equal(
                "edited-end",
                correction
                    .CorrectedEndedAtText);

            Assert.Equal(
                "edited-reason",
                correction.Reason);
        });
    }

    [Fact]
    [Trait("Task11Cycle", "C2")]
    public void C2_sessions_view_code_behind_loads_history_and_selects_recent_session()
    {
        var code =
            ReadRepositoryFile(
                "src",
                "PlayStead.UI",
                "Sessions",
                "SessionsView.xaml.cs");

        Assert.Contains(
            "await viewModel.RefreshAsync(",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "RecentSessionDetailsButton_OnClick",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "await viewModel.SelectRecentSessionAsync(",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Task11Cycle", "C2")]
    public void C2_detail_view_code_behind_saves_through_correction_view_model()
    {
        var xaml =
            ReadRepositoryFile(
                "src",
                "PlayStead.UI",
                "Sessions",
                "SessionDetailView.xaml");

        var code =
            ReadRepositoryFile(
                "src",
                "PlayStead.UI",
                "Sessions",
                "SessionDetailView.xaml.cs");

        Assert.Contains(
            "CorrectSessionButton_OnClick",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "SaveCorrectionButton_OnClick",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "SaveCorrectionButton_OnClick",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "await viewModel.Correction.SaveAsync(",
            code,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(
        params string[] relativeParts) =>
        File.ReadAllText(
            FindRepositoryFile(
                relativeParts));

    private static string FindRepositoryFile(
        params string[] relativeParts)
    {
        var starts =
            new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory
            };

        foreach (var start in starts)
        {
            var current =
                new DirectoryInfo(
                    Path.GetFullPath(
                        start));

            while (current is not null)
            {
                var candidateParts =
                    new string[
                        relativeParts.Length + 1];

                candidateParts[0] =
                    current.FullName;

                Array.Copy(
                    relativeParts,
                    0,
                    candidateParts,
                    1,
                    relativeParts.Length);

                var candidate =
                    Path.Combine(
                        candidateParts);

                if (File.Exists(
                        candidate))
                {
                    return candidate;
                }

                current =
                    current.Parent;
            }
        }

        return Path.Combine(
            relativeParts);
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
                .Capture(
                    failure)
                .Throw();
        }
    }

    private sealed class FakeSessionsState
    {
        public FakeSessionsState(
            object? selectedSessionDetail)
        {
            SelectedSessionDetail =
                selectedSessionDetail;
        }

        public bool HasActiveSessions =>
            false;

        public bool HasRecentSessions =>
            false;

        public IReadOnlyList<object>
            ActiveSessions =>
                [];

        public IReadOnlyList<object>
            RecentSessions =>
                [];

        public object?
            SelectedSessionDetail
        {
            get;
        }
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
        FakeCorrectionState Correction)
    {
        public static FakeDetailState Create(
            FakeCorrectionState? correction = null) =>
            new(
                "Test",
                "observed-start",
                "observed-end",
                "observed-duration",
                "effective-start",
                "effective-end",
                "effective-duration",
                "ProcessExited",
                false,
                false,
                correction ??
                    new FakeCorrectionState());
    }

    private sealed class FakeCorrectionState :
        INotifyPropertyChanged
    {
        private string _correctedStartedAtText =
            string.Empty;

        private string _correctedEndedAtText =
            string.Empty;

        private string? _reason;

        public event PropertyChangedEventHandler?
            PropertyChanged;

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

        public bool CanSave =>
            true;

        public string? ValidationMessage =>
            null;

        private void OnPropertyChanged(
            [CallerMemberName]
            string? propertyName = null) =>
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    propertyName));
    }
}
