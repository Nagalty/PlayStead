using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Sessions;

public sealed record ProcessSignatureEntry(
    string ExecutableName,
    ProcessSignatureEntryKind Kind,
    string? ExecutablePath = null,
    FileRevision? ValidatedRevision = null);
