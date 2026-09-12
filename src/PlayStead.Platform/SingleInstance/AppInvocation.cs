namespace PlayStead.Platform.SingleInstance;

public sealed record AppInvocation(
    bool Activate,
    string? DeepLink)
{
    public static AppInvocation Default { get; } =
        new(
            Activate: true,
            DeepLink: null);
}
