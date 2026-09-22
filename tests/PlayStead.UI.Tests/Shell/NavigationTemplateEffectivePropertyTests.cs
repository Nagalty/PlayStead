using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PlayStead.UI.Tests.TestSupport;

namespace PlayStead.UI.Tests.Shell;

[Collection(PlaySteadWpfApplicationCollection.Name)]
public sealed class NavigationTemplateEffectivePropertyTests
{
    [Fact]
    public void Navigation_template_exposes_effective_active_bottom_border()
    {
        RunSta(() =>
        {
            var window = new MainWindow();
            try
            {
                var button = Assert.IsType<Button>(window.FindName("LibraryNavButton"));
                button.Style = Assert.IsType<Style>(
                    Application.Current!.Resources["PlayStead.Button.Navigation"]);
                button.ApplyTemplate();

                var border = Assert.IsType<Border>(
                    button.Template!.FindName("BackgroundBorder", button));

                button.Tag = false;
                Assert.Equal(new Thickness(0), border.BorderThickness);
                Assert.Equal(
                    ((CornerRadius)Application.Current.Resources["PlayStead.Radius.Small"]).TopLeft,
                    border.CornerRadius.TopLeft);

                button.Tag = true;
                Assert.Equal(new Thickness(0, 0, 0, 2), border.BorderThickness);
                Assert.Equal(
                    ((SolidColorBrush)Application.Current!.Resources["PlayStead.Brush.Accent"]).Color,
                    Assert.IsType<SolidColorBrush>(border.BorderBrush).Color);

                Assert.Equal(
                    ((SolidColorBrush)Application.Current.Resources["PlayStead.Brush.SurfaceRaised"]).Color,
                    Assert.IsType<SolidColorBrush>(border.Background).Color);

                Assert.Equal(new Thickness(0, 0, 0, 2), border.BorderThickness);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunSta(Action action) => PlaySteadWpfTestResources.Run(action);
}
