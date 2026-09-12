namespace PlayStead.Core.Sessions;

public sealed record ProcessSignatureEntry(
    string ExecutableName,
    ProcessSignatureEntryKind Kind);
