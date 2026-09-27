using System.Windows;
using System.Windows.Media;
using PlayStead.UI.Behaviors;

namespace PlayStead.UI.Tests.Home;

public sealed class RoundedClipBehaviorTests
{
    [Fact]
    public void CreateGeometry_uses_current_size_and_radius()
    {
        var geometry = RoundedClipBehavior.CreateGeometry(275, 165, new CornerRadius(8));

        Assert.Equal(new Rect(0, 0, 275, 165), geometry.Rect);
        Assert.Equal(8, geometry.RadiusX);
        Assert.Equal(8, geometry.RadiusY);
    }

    [Fact]
    public void CreateGeometry_returns_empty_geometry_for_unmeasured_element()
    {
        var geometry = RoundedClipBehavior.CreateGeometry(0, 0, new CornerRadius(8));

        Assert.Equal(Rect.Empty, geometry.Rect);
    }
}
