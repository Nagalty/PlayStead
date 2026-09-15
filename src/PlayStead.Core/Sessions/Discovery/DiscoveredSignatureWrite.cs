using PlayStead.Core.Library;

namespace PlayStead.Core.Sessions.Discovery;

public sealed record DiscoveredSignatureExpectation(
    Guid ConcurrencyToken,
    Guid? GenerationId,
    ProcessSignatureValidationState ValidationState,
    InstallationId? InstallationId);

public sealed record DiscoveredSignatureWrite(
    ProcessSignature Signature,
    Guid ExpectedLearningToken,
    Guid ReferenceEpisodeId,
    Guid ConfirmationEpisodeId);
