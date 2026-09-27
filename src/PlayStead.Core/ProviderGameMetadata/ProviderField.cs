namespace PlayStead.Core.ProviderGameMetadata;

public enum ProviderFieldState
{
    NotReported = 0,
    Value = 1,
    ExplicitUnknown = 2
}

public readonly record struct ProviderField<T>(ProviderFieldState State, T? Value)
{
    public static ProviderField<T> NotReported => new(ProviderFieldState.NotReported, default);
    public static ProviderField<T> ExplicitUnknown => new(ProviderFieldState.ExplicitUnknown, default);
    public static ProviderField<T> FromValue(T value) => new(ProviderFieldState.Value, value);
}
