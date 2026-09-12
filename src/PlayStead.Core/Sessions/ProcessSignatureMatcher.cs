namespace PlayStead.Core.Sessions;

public sealed class ProcessSignatureMatcher
{
    private static readonly StringComparer ExecutableComparer =
        StringComparer.OrdinalIgnoreCase;

    public ProcessSignatureMatch Match(
        ProcessSignature signature,
        IReadOnlyCollection<ProcessSnapshot> processes)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(processes);

        var effectiveKinds = BuildEffectiveKinds(signature.Entries);

        var main = new List<ProcessSnapshot>();
        var auxiliary = new List<ProcessSnapshot>();
        var excluded = new List<ProcessSnapshot>();

        foreach (var process in processes)
        {
            if (string.IsNullOrWhiteSpace(process.ExecutableName) ||
                !effectiveKinds.TryGetValue(process.ExecutableName, out var kind))
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

    private static Dictionary<string, ProcessSignatureEntryKind> BuildEffectiveKinds(
        IReadOnlyList<ProcessSignatureEntry> entries)
    {
        var result = new Dictionary<string, ProcessSignatureEntryKind>(
            ExecutableComparer);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.ExecutableName))
            {
                continue;
            }

            if (!result.TryGetValue(entry.ExecutableName, out var existing) ||
                Priority(entry.Kind) > Priority(existing))
            {
                result[entry.ExecutableName] = entry.Kind;
            }
        }

        return result;
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
