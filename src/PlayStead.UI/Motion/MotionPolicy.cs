namespace PlayStead.UI.Motion;

public static class MotionPolicy
{
    public static TimeSpan Resolve(
        TimeSpan normalDuration,
        bool reduceMotion) =>
        reduceMotion
            ? TimeSpan.Zero
            : normalDuration;
}
