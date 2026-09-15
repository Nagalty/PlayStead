using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Sessions;

public sealed class ProcessSignatureMatcher
{
    public ProcessSignatureMatch Match(
        ProcessSignature signature,
        IReadOnlyCollection<ProcessSnapshot> processes)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(processes);

        var main = new List<ProcessSnapshot>();
        var auxiliary = new List<ProcessSnapshot>();
        var excluded = new List<ProcessSnapshot>();

        if (!Enum.IsDefined(signature.Origin) ||
            signature.Origin == ProcessSignatureOrigin.Discovered && !IsDiscoveredAdmissible(signature))
            return new ProcessSignatureMatch(signature.GameId, main, auxiliary, excluded);

        foreach (var process in processes)
        {
            ProcessSignatureEntryKind? kind = null;
            foreach (var entry in signature.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.ExecutableName) ||
                    !string.Equals(entry.ExecutableName, process.ExecutableName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var identityMatches = !string.IsNullOrWhiteSpace(entry.ExecutablePath)
                    ? string.Equals(entry.ExecutablePath, process.ExecutablePath, StringComparison.OrdinalIgnoreCase)
                    : signature.Origin is ProcessSignatureOrigin.Manual or ProcessSignatureOrigin.BuiltIn;
                if (identityMatches && (kind is null || Priority(entry.Kind) > Priority(kind.Value)))
                    kind = entry.Kind;
            }
            if (kind is null)
            {
                continue;
            }

            switch (kind)
            {
                case ProcessSignatureEntryKind.Excluded:
                    excluded.Add(process);
                    break;

                case ProcessSignatureEntryKind.Main:
                    main.Add(process);
                    break;

                case ProcessSignatureEntryKind.Auxiliary:
                    auxiliary.Add(process);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(signature),
                        kind,
                        "Unsupported process signature entry kind.");
            }
        }

        return new ProcessSignatureMatch(
            signature.GameId,
            main,
            auxiliary,
            excluded);
    }

    internal static bool IsDiscoveredAdmissible(ProcessSignature signature)
    {
        return signature.Origin == ProcessSignatureOrigin.Discovered &&
            signature.Discovery is { ValidationState: ProcessSignatureValidationState.Valid } metadata &&
            metadata.InstallationId is { } installation && installation.Value != Guid.Empty &&
            metadata.GenerationId is { } generation && generation != Guid.Empty &&
            metadata.PolicyVersion == ProcessSignatureDiscoveryPolicy.CurrentPolicyVersion &&
            metadata.ConcurrencyToken != Guid.Empty && signature.Entries.Count > 0 &&
            signature.Entries.All(entry => Enum.IsDefined(entry.Kind) && entry.ValidatedRevision is not null &&
                HasCanonicalIdentity(entry));
    }

    // Only inspect the supplied identity. Never resolve or normalize untrusted paths here.
    private static bool HasCanonicalIdentity(ProcessSignatureEntry entry)
    {
        var path = entry.ExecutablePath;
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(entry.ExecutableName)) return false;
        var rooted = path.Length > 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\' ||
            path.StartsWith(@"\\", StringComparison.Ordinal) && path.Split('\\').Length >= 5;
        return rooted && !path.Split('\\', '/').Any(part => part is "." or "..") &&
            string.Equals(path[(path.LastIndexOf('\\') + 1)..], entry.ExecutableName, StringComparison.OrdinalIgnoreCase);
    }

    private static int Priority(ProcessSignatureEntryKind kind) =>
        kind switch
        {
            ProcessSignatureEntryKind.Auxiliary => 1,
            ProcessSignatureEntryKind.Main => 2,
            ProcessSignatureEntryKind.Excluded => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
