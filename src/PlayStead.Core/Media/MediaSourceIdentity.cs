using PlayStead.Core.Library;

namespace PlayStead.Core.Media;

public sealed record MediaSourceIdentity
{
    public ProviderKind Provider { get; }
    public string ExternalId { get; }

    public MediaSourceIdentity(ProviderKind provider, string externalId)
    {
        Provider = provider;
        ExternalId = Validate(externalId);
    }

    private static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Contains('/') || value.Contains('\\') || value.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Media source ID must be a safe path segment.", nameof(value));
        return value.Trim();
    }
}
