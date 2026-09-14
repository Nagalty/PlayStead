using System.Collections.ObjectModel;
using PlayStead.Core.Library;

namespace PlayStead.Core.Media;

public sealed record GameMediaIdentity
{
    public GameMediaIdentity(
        ProviderKind provider,
        string providerGameId,
        string canonicalTitle,
        IReadOnlyDictionary<string, string>? externalIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerGameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalTitle);

        if (provider == ProviderKind.Steam &&
            !providerGameId.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(
                "Steam media identity requires a numeric AppID.",
                nameof(providerGameId));
        }

        Provider = provider;
        ProviderGameId = providerGameId.Trim();
        CanonicalTitle = canonicalTitle.Trim();
        ExternalIds = new ReadOnlyDictionary<string, string>(
            externalIds is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(externalIds));
    }

    public ProviderKind Provider { get; }
    public string ProviderGameId { get; }
    public string CanonicalTitle { get; }
    public IReadOnlyDictionary<string, string> ExternalIds { get; }
}
