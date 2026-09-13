using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;

namespace PlayStead.UI.Tests.Sessions;

public sealed class SessionDetailViewStateTests
{
    [Fact]
    [Trait("Task11Cycle", "A")]
    public void Detail_view_contract_is_constructible_on_STA_with_detail_and_editor_controls()
    {
        var type = SessionCycleAContract.RequireType("SessionDetailView");
        Assert.True(typeof(UserControl).IsAssignableFrom(type));
        var constructor = type.GetConstructor(Type.EmptyTypes);
        Assert.NotNull(constructor);

        RunSta(() =>
        {
            var view = Assert.IsAssignableFrom<UserControl>(constructor.Invoke(null));

            // These names are the observation points for future Cycle C binding/action tests.
            foreach (var name in new[]
            {
                "DetailTitle", "ObservedStartText", "ObservedEndText", "ObservedDurationText",
                "EffectiveStartText", "EffectiveEndText", "EffectiveDurationText", "EndReasonText",
                "CorrectionValidationText"
            })
            {
                Assert.IsType<TextBlock>(view.FindName(name));
            }

            foreach (var name in new[] { "RecoveryBadge", "CorrectionBadge", "CorrectionPanel" })
            {
                Assert.IsAssignableFrom<FrameworkElement>(view.FindName(name));
            }

            foreach (var name in new[]
            {
                "CorrectionStartTextBox", "CorrectionEndTextBox", "CorrectionReasonTextBox"
            })
            {
                Assert.IsType<TextBox>(view.FindName(name));
            }

            Assert.IsType<Button>(view.FindName("CorrectSessionButton"));
            Assert.IsType<Button>(view.FindName("SaveCorrectionButton"));
        });
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
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

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
