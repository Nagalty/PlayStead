using PlayStead.Core.Library;

namespace PlayStead.Core.Sessions;

public sealed record DiscoveredSignatureMetadata(
    InstallationId? InstallationId,
    Guid? GenerationId,
    int? PolicyVersion,
    ProcessSignatureValidationState ValidationState,
    Guid ConcurrencyToken);
