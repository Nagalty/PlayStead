using PlayStead.Core.Sessions;

namespace PlayStead.Core.Sessions.Discovery;

public sealed record DiscoveryEvaluation
{
    public DiscoveryEvaluation(
        ExecutableInventory inventory,
        IReadOnlyList<LearningEpisodeSummary> episodes,
        bool hasAmbiguousInstallation,
        ProcessSignatureOrigin? existingSignatureOrigin)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(episodes);
        if (existingSignatureOrigin.HasValue && !Enum.IsDefined(existingSignatureOrigin.Value))
            throw new ArgumentOutOfRangeException(nameof(existingSignatureOrigin));

        var episodeCopy = episodes.ToArray();
        if (episodeCopy.Any(episode => episode is null))
            throw new ArgumentException("Episodes must not contain null.", nameof(episodes));

        Inventory = inventory;
        Episodes = Array.AsReadOnly(episodeCopy);
        HasAmbiguousInstallation = hasAmbiguousInstallation;
        ExistingSignatureOrigin = existingSignatureOrigin;
    }

    public ExecutableInventory Inventory { get; }
    public IReadOnlyList<LearningEpisodeSummary> Episodes { get; }
    public bool HasAmbiguousInstallation { get; }
    public ProcessSignatureOrigin? ExistingSignatureOrigin { get; }
}
