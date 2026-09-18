using System.Runtime.ExceptionServices;
using System.Windows;
using PlayStead.UI.Controls;

namespace PlayStead.UI.Tests.Themes;

public sealed class PlaySteadXamlResourceSmokeTests
{
    [Fact]
    public void Authoritative_resource_dictionaries_load_with_typed_values()
    {
        RunSta(() =>
        {
            var tokens = (ResourceDictionary)Application.LoadComponent(
                new Uri(
                    "/PlayStead.UI;component/Themes/PlaySteadTokens.xaml",
                    UriKind.Relative));
            var controls = (ResourceDictionary)Application.LoadComponent(
                new Uri(
                    "/PlayStead.UI;component/Themes/PlaySteadControls.xaml",
                    UriKind.Relative));

            Assert.IsType<Thickness>(tokens["PlayStead.Inset.Card"]);
            Assert.IsType<CornerRadius>(tokens["PlayStead.Radius.Medium"]);
            Assert.IsType<System.Windows.Media.FontFamily>(tokens["PlayStead.Font.Sans"]);
            Assert.IsType<double>(tokens["PlayStead.Control.Height"]);
            Assert.NotEmpty(controls);
        });
    }

    [Fact]
    public void Representative_resource_consumers_initialize_without_XamlParseException()
    {
        RunSta(() =>
        {
            Assert.NotNull(new EmptyState());
            Assert.NotNull(new GameCard());
            Assert.NotNull(new PlaySplitButton());
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
