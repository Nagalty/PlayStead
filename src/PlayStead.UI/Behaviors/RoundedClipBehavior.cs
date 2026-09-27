using System.Windows;
using System.Windows.Media;

namespace PlayStead.UI.Behaviors;

/// <summary>
/// Clips a content root to its actual rounded bounds, keeping the geometry
/// correct when the card is measured at a different size or DPI.
/// </summary>
public static class RoundedClipBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(RoundedClipBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached(
            "CornerRadius",
            typeof(CornerRadius),
            typeof(RoundedClipBehavior),
            new PropertyMetadata(new CornerRadius(8), OnCornerRadiusChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetCornerRadius(DependencyObject element, CornerRadius value) =>
        element.SetValue(CornerRadiusProperty, value);

    public static CornerRadius GetCornerRadius(DependencyObject element) =>
        (CornerRadius)element.GetValue(CornerRadiusProperty);

    public static RectangleGeometry CreateGeometry(double width, double height, CornerRadius radius)
    {
        if (width <= 0 || height <= 0)
        {
            return new RectangleGeometry(Rect.Empty);
        }

        var effectiveRadius = Math.Max(0, Math.Min(
            Math.Min(radius.TopLeft, radius.TopRight),
            Math.Min(radius.BottomRight, radius.BottomLeft)));
        effectiveRadius = Math.Min(effectiveRadius, Math.Min(width, height) / 2);
        return new RectangleGeometry(new Rect(0, 0, width, height), effectiveRadius, effectiveRadius);
    }

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if ((bool)args.OldValue)
        {
            element.Loaded -= OnLoaded;
            element.Unloaded -= OnUnloaded;
            element.SizeChanged -= OnSizeChanged;
            element.ClearValue(UIElement.ClipProperty);
        }

        if ((bool)args.NewValue)
        {
            element.Loaded += OnLoaded;
            element.Unloaded += OnUnloaded;
            element.SizeChanged += OnSizeChanged;
            UpdateClip(element);
        }
    }

    private static void OnCornerRadiusChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is FrameworkElement element && GetIsEnabled(element))
        {
            UpdateClip(element);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement element)
        {
            element.SizeChanged -= OnSizeChanged;
            element.SizeChanged += OnSizeChanged;
            UpdateClip(element);
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement element)
        {
            element.SizeChanged -= OnSizeChanged;
        }
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (sender is FrameworkElement element)
        {
            UpdateClip(element);
        }
    }

    private static void UpdateClip(FrameworkElement element)
    {
        element.Clip = CreateGeometry(element.ActualWidth, element.ActualHeight, GetCornerRadius(element));
    }
}
