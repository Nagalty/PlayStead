using System.Runtime.CompilerServices;
using System.Windows;

namespace PlayStead.UI.Settings;

public sealed class UiMotionController
{
    private const string FastKey = "PlayStead.Duration.Fast";
    private const string NormalKey = "PlayStead.Duration.Normal";

    private readonly ConditionalWeakTable<ResourceDictionary, NominalDurations>
        _nominalDurations = new();

    public void Apply(
        ResourceDictionary resources,
        bool reduceMotion)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var fast = ReadDuration(resources, FastKey);
        var normal = ReadDuration(resources, NormalKey);

        var nominal = _nominalDurations.GetValue(
            resources,
            _ => new NominalDurations(fast, normal));

        resources[FastKey] = reduceMotion
            ? new Duration(TimeSpan.Zero)
            : nominal.Fast;

        resources[NormalKey] = reduceMotion
            ? new Duration(TimeSpan.Zero)
            : nominal.Normal;
    }

    private static Duration ReadDuration(
        ResourceDictionary resources,
        string key)
    {
        if (!resources.Contains(key) ||
            resources[key] is not Duration duration ||
            !duration.HasTimeSpan)
        {
            throw new InvalidOperationException(
                $"Resource '{key}' must contain a finite Duration.");
        }

        return duration;
    }

    private sealed record NominalDurations(
        Duration Fast,
        Duration Normal);
}
