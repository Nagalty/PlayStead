using System.Windows;
using PlayStead.UI.Settings;

namespace PlayStead.UI.Tests.Settings;

public sealed class UiMotionControllerTests
{
    [Fact]
    public void Apply_true_sets_global_animation_durations_to_zero()
    {
        var resources =
            CreateResources();

        var sut =
            new UiMotionController();

        sut.Apply(
            resources,
            reduceMotion: true);

        Assert.Equal(
            TimeSpan.Zero,
            GetDuration(
                resources,
                "PlayStead.Duration.Fast"));

        Assert.Equal(
            TimeSpan.Zero,
            GetDuration(
                resources,
                "PlayStead.Duration.Normal"));
    }

    [Fact]
    public void Apply_false_keeps_nominal_durations_when_motion_is_not_reduced()
    {
        var resources =
            CreateResources();

        var sut =
            new UiMotionController();

        sut.Apply(
            resources,
            reduceMotion: false);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                120),
            GetDuration(
                resources,
                "PlayStead.Duration.Fast"));

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                180),
            GetDuration(
                resources,
                "PlayStead.Duration.Normal"));
    }

    [Fact]
    public void Apply_false_after_true_restores_the_original_nominal_durations()
    {
        var resources =
            CreateResources();

        var sut =
            new UiMotionController();

        sut.Apply(
            resources,
            reduceMotion: true);

        sut.Apply(
            resources,
            reduceMotion: false);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                120),
            GetDuration(
                resources,
                "PlayStead.Duration.Fast"));

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                180),
            GetDuration(
                resources,
                "PlayStead.Duration.Normal"));
    }

    [Fact]
    public void Repeated_apply_true_is_idempotent()
    {
        var resources =
            CreateResources();

        var sut =
            new UiMotionController();

        sut.Apply(
            resources,
            reduceMotion: true);

        sut.Apply(
            resources,
            reduceMotion: true);

        Assert.Equal(
            TimeSpan.Zero,
            GetDuration(
                resources,
                "PlayStead.Duration.Fast"));

        Assert.Equal(
            TimeSpan.Zero,
            GetDuration(
                resources,
                "PlayStead.Duration.Normal"));
    }

    private static ResourceDictionary
        CreateResources()
    {
        return new ResourceDictionary
        {
            ["PlayStead.Duration.Fast"] =
                new Duration(
                    TimeSpan.FromMilliseconds(
                        120)),

            ["PlayStead.Duration.Normal"] =
                new Duration(
                    TimeSpan.FromMilliseconds(
                        180))
        };
    }

    private static TimeSpan GetDuration(
        ResourceDictionary resources,
        string key)
    {
        var duration =
            Assert.IsType<Duration>(
                resources[key]);

        Assert.True(
            duration.HasTimeSpan);

        return duration.TimeSpan;
    }
}
