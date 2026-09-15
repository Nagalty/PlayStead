using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Platform.Processes.Discovery;

public sealed class WindowsExecutableInventorySource : IExecutableInventorySource
{
    private readonly Func<string, IEnumerable<string>> _enumerateEntries;
    private readonly Func<string, FileAttributes> _readAttributes;
    private readonly Func<string, FileRevision> _readRevision;

    public WindowsExecutableInventorySource()
        : this(Directory.EnumerateFileSystemEntries, File.GetAttributes, ReadRevision)
    {
    }

    public WindowsExecutableInventorySource(
        Func<string, IEnumerable<string>> enumerateEntries,
        Func<string, FileAttributes> readAttributes,
        Func<string, FileRevision> readRevision)
    {
        ArgumentNullException.ThrowIfNull(enumerateEntries);
        ArgumentNullException.ThrowIfNull(readAttributes);
        ArgumentNullException.ThrowIfNull(readRevision);

        _enumerateEntries = enumerateEntries;
        _readAttributes = readAttributes;
        _readRevision = readRevision;
    }

    public Task<ExecutableInventory> InventoryAsync(
        InstallationScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(scope);

        var candidates = new List<ExecutableCandidate>();
        var issues = new List<InventoryIssue>();
        string root;
        try
        {
            root = WindowsExecutablePath.NormalizeRoot(scope.RootPath);
        }
        catch (ArgumentException)
        {
            issues.Add(new InventoryIssue(scope.RootPath, InventoryIssueKind.InvalidRoot));
            return Task.FromResult(BuildInventory(scope, candidates, issues, cancellationToken));
        }

        var canonicalScope = new InstallationScope(scope.GameId, scope.InstallationId,
            root, scope.GenerationId, scope.IsPresent);
        if (!CheckDirectoryComponents(root, root, issues, cancellationToken))
            return Task.FromResult(BuildInventory(canonicalScope, candidates, issues, cancellationToken));

        var pending = new Stack<string>();
        var traversedDirectories = new List<string>();
        var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push(root);
        while (pending.Count != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            if (!CheckDirectoryComponents(directory, root, issues, cancellationToken))
                continue;
            traversedDirectories.Add(directory);
            try
            {
                foreach (var rawEntry in _enumerateEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReadEntry(root, directory, rawEntry, seenEntries, pending, candidates, issues,
                        cancellationToken);
                }
            }
            catch (Exception error) when (IsExpectedIoError(error))
            {
                issues.Add(new InventoryIssue(directory, IssueFor(error, isRootAccess: directory == root)));
            }
        }

        RevalidateKnownPaths(root, traversedDirectories, candidates, issues, cancellationToken);
        return Task.FromResult(BuildInventory(canonicalScope, candidates, issues, cancellationToken));
    }

    private void RevalidateKnownPaths(string root, List<string> traversedDirectories,
        List<ExecutableCandidate> candidates, List<InventoryIssue> issues,
        CancellationToken cancellationToken)
    {
        var unsafeDirectories = new List<string>();
        foreach (var directory in traversedDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CheckDirectoryComponents(directory, root, issues, cancellationToken))
                unsafeDirectories.Add(directory);
        }

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (unsafeDirectories.Any(directory =>
                    WindowsExecutablePath.IsStrictlyUnderRoot(directory, candidate.ExecutablePath)))
                continue;

            var parent = Path.GetDirectoryName(candidate.ExecutablePath)!;
            if (!CheckDirectoryComponents(parent, root, issues, cancellationToken))
                continue;

            FileAttributes attributes;
            try
            {
                attributes = _readAttributes(candidate.ExecutablePath);
            }
            catch (Exception error) when (IsExpectedIoError(error))
            {
                issues.Add(new InventoryIssue(candidate.ExecutablePath,
                    IssueFor(error, isRootAccess: false)));
                continue;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                issues.Add(new InventoryIssue(candidate.ExecutablePath, InventoryIssueKind.ReparsePoint));
                continue;
            }
            if ((attributes & FileAttributes.Directory) != 0)
            {
                issues.Add(new InventoryIssue(candidate.ExecutablePath, InventoryIssueKind.InvalidRoot));
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var currentRevision = _readRevision(candidate.ExecutablePath);
                if (currentRevision != candidate.Revision)
                    issues.Add(new InventoryIssue(candidate.ExecutablePath,
                        InventoryIssueKind.RevisionChanged));
            }
            catch (Exception error) when (IsExpectedIoError(error))
            {
                issues.Add(new InventoryIssue(candidate.ExecutablePath,
                    IssueFor(error, isRootAccess: false)));
            }
        }
    }

    private bool CheckDirectoryComponents(string directory, string root, List<InventoryIssue> issues,
        CancellationToken cancellationToken)
    {
        var volume = Path.GetPathRoot(directory)!;
        var component = volume;
        var parts = directory[volume.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        for (var index = -1; index < parts.Length; index++)
        {
            if (index >= 0)
                component = Path.Combine(component, parts[index]);

            cancellationToken.ThrowIfCancellationRequested();
            FileAttributes attributes;
            try
            {
                attributes = _readAttributes(component);
            }
            catch (Exception error) when (IsExpectedIoError(error))
            {
                issues.Add(new InventoryIssue(component,
                    IssueFor(error, isRootAccess: component.Length <= root.Length)));
                return false;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                issues.Add(new InventoryIssue(component, InventoryIssueKind.ReparsePoint));
                return false;
            }
            if ((attributes & FileAttributes.Directory) == 0)
            {
                issues.Add(new InventoryIssue(component, InventoryIssueKind.InvalidRoot));
                return false;
            }
        }

        return true;
    }

    private void ReadEntry(string root, string directory, string rawEntry,
        HashSet<string> seenEntries,
        Stack<string> pending, List<ExecutableCandidate> candidates,
        List<InventoryIssue> issues, CancellationToken cancellationToken)
    {
        if (!WindowsExecutablePath.IsStrictlyUnderRoot(root, rawEntry))
        {
            issues.Add(new InventoryIssue(rawEntry, InventoryIssueKind.EscapedRoot));
            return;
        }

        var entry = WindowsExecutablePath.NormalizeRoot(rawEntry);
        if (!string.Equals(Path.GetDirectoryName(entry), directory,
                StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new InventoryIssue(entry, InventoryIssueKind.EscapedRoot));
            return;
        }
        if (!seenEntries.Add(entry))
        {
            issues.Add(new InventoryIssue(entry, InventoryIssueKind.InvalidRoot));
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        FileAttributes attributes;
        try
        {
            attributes = _readAttributes(entry);
        }
        catch (Exception error) when (IsExpectedIoError(error))
        {
            issues.Add(new InventoryIssue(entry, IssueFor(error, isRootAccess: false)));
            return;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            issues.Add(new InventoryIssue(entry, InventoryIssueKind.ReparsePoint));
            return;
        }
        if ((attributes & FileAttributes.Directory) != 0)
        {
            pending.Push(entry);
            return;
        }
        if (!Path.GetExtension(entry).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return;

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var revision = _readRevision(entry);
            candidates.Add(new ExecutableCandidate(entry, Path.GetFileName(entry), revision));
        }
        catch (Exception error) when (IsExpectedIoError(error))
        {
            issues.Add(new InventoryIssue(entry, IssueFor(error, isRootAccess: false)));
        }
    }

    private static ExecutableInventory BuildInventory(InstallationScope scope,
        List<ExecutableCandidate> candidates, List<InventoryIssue> issues,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var orderedCandidates = candidates
            .OrderBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.ExecutablePath, StringComparer.Ordinal)
            .ToArray();
        var orderedIssues = issues.Distinct()
            .OrderBy(issue => issue.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Path, StringComparer.Ordinal)
            .ThenBy(issue => issue.Kind)
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new ExecutableInventory(scope,
            orderedIssues.Length == 0 ? InventoryCompleteness.Complete : InventoryCompleteness.Incomplete,
            orderedCandidates, orderedIssues);
    }

    private static bool IsExpectedIoError(Exception error) =>
        error is UnauthorizedAccessException or DirectoryNotFoundException or
            FileNotFoundException or IOException;

    private static InventoryIssueKind IssueFor(Exception error, bool isRootAccess) =>
        error switch
        {
            UnauthorizedAccessException => InventoryIssueKind.AccessDenied,
            DirectoryNotFoundException or FileNotFoundException =>
                isRootAccess ? InventoryIssueKind.MissingRoot : InventoryIssueKind.IoFailure,
            IOException => InventoryIssueKind.IoFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(error))
        };

    private static FileRevision ReadRevision(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var info = new FileInfo(path);
        info.Refresh();
        if (!info.Exists)
            throw new FileNotFoundException("Executable disappeared.", path);
        return new FileRevision(stream.Length,
            new DateTimeOffset(info.LastWriteTimeUtc));
    }
}
