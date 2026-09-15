namespace PlayStead.Core.Sessions;

public enum DiscoveredSignatureValidationResult { Pending = 0, Valid = 1, Invalid = 2 }

public interface IDiscoveredSignatureValidator
{
    Task<DiscoveredSignatureValidationResult> ValidateAsync(
        ProcessSignature signature, CancellationToken cancellationToken);
}
