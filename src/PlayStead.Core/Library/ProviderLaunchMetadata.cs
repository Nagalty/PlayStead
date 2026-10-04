namespace PlayStead.Core.Library;

public sealed record ProviderLaunchMetadata(
    ProviderKind Provider,
    IReadOnlyDictionary<string, string> Values)
{
    public string? this[string key] =>
        Values.TryGetValue(key, out var value) ? value : null;
}
