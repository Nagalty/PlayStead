using System.Reflection;

namespace PlayStead.UI.Tests.Motion;

public sealed class MotionPolicyTests
{
    [Fact]
    public void Resolve_returns_zero_for_decorative_duration_when_reduce_motion_is_enabled()
    {
        var resolve =
            RequireResolveMethod();

        var normal =
            TimeSpan.FromMilliseconds(
                180);

        var result =
            Assert.IsType<TimeSpan>(
                resolve.Invoke(
                    null,
                    new object[]
                    {
                        normal,
                        true
                    }));

        Assert.Equal(
            TimeSpan.Zero,
            result);
    }

    [Fact]
    public void Resolve_preserves_normal_duration_when_reduce_motion_is_disabled()
    {
        var resolve =
            RequireResolveMethod();

        var normal =
            TimeSpan.FromMilliseconds(
                180);

        var result =
            Assert.IsType<TimeSpan>(
                resolve.Invoke(
                    null,
                    new object[]
                    {
                        normal,
                        false
                    }));

        Assert.Equal(
            normal,
            result);
    }

    private static MethodInfo RequireResolveMethod()
    {
        var type =
            typeof(MainWindow)
                .Assembly
                .GetType(
                    "PlayStead.UI.Motion.MotionPolicy",
                    throwOnError: false,
                    ignoreCase: false);

        Assert.True(
            type is not null,
            "Task 11 requires PlayStead.UI.Motion.MotionPolicy.");

        var method =
            type!.GetMethod(
                "Resolve",
                BindingFlags.Public |
                BindingFlags.Static,
                binder: null,
                types:
                [
                    typeof(TimeSpan),
                    typeof(bool)
                ],
                modifiers: null);

        Assert.True(
            method is not null,
            "MotionPolicy must expose public static TimeSpan Resolve(TimeSpan normalDuration, bool reduceMotion).");

        Assert.Equal(
            typeof(TimeSpan),
            method!.ReturnType);

        return method;
    }
}
