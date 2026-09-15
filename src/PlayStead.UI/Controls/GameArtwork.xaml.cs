using System.Windows;
using System.Windows.Controls;

namespace PlayStead.UI.Controls;

public partial class GameArtwork :
    UserControl
{
    public static readonly DependencyProperty SourcePathProperty =
        DependencyProperty.Register(
            nameof(SourcePath),
            typeof(string),
            typeof(GameArtwork),
            new PropertyMetadata(null));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(GameArtwork),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ProviderLabelProperty =
        DependencyProperty.Register(
            nameof(ProviderLabel),
            typeof(string),
            typeof(GameArtwork),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HasArtworkProperty =
        DependencyProperty.Register(
            nameof(HasArtwork),
            typeof(bool),
            typeof(GameArtwork),
            new PropertyMetadata(false));

    public GameArtwork()
    {
        InitializeComponent();
    }

    public string? SourcePath
    {
        get => (string?)GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string ProviderLabel
    {
        get => (string)GetValue(ProviderLabelProperty);
        set => SetValue(ProviderLabelProperty, value);
    }

    public bool HasArtwork
    {
        get => (bool)GetValue(HasArtworkProperty);
        set => SetValue(HasArtworkProperty, value);
    }
}
