namespace PlayStead.Core.ProviderGameMetadata;

public sealed record ProviderGameMetadataProgress(
    bool IsRunning,
    int Total,
    int Completed,
    int Succeeded,
    int Failed);

public interface IProviderGameMetadataProgress
{
    ProviderGameMetadataProgress Current { get; }
    event EventHandler<ProviderGameMetadataProgress>? ProgressChanged;
}
