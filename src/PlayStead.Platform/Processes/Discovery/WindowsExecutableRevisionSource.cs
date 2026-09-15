using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Platform.Processes.Discovery;

public sealed class WindowsExecutableRevisionSource : IExecutableRevisionSource
{
    private readonly Func<string, FileAttributes> _readAttributes;
    private readonly Func<string, FileRevision> _readRevision;

    public WindowsExecutableRevisionSource() : this(File.GetAttributes, ReadRevision)
    {
    }

    public WindowsExecutableRevisionSource(
        Func<string, FileAttributes> readAttributes,
        Func<string, FileRevision> readRevision)
    {
        ArgumentNullException.ThrowIfNull(readAttributes);
        ArgumentNullException.ThrowIfNull(readRevision);
        _readAttributes = readAttributes;
        _readRevision = readRevision;
    }

    public Task<ExecutableRevisionResult> ReadAsync(
        InstallationScope scope, string executablePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        cancellationToken.ThrowIfCancellationRequested();

        string root;
        try
        {
            root = WindowsExecutablePath.NormalizeRoot(scope.RootPath);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(Failure(scope.RootPath, InventoryIssueKind.InvalidRoot));
        }

        if (!scope.IsPresent)
            return Task.FromResult(Failure(root, InventoryIssueKind.MissingRoot));

        if (!WindowsExecutablePath.IsStrictlyUnderRoot(root, executablePath))
            return Task.FromResult(Failure(executablePath, InventoryIssueKind.EscapedRoot));

        var target = WindowsExecutablePath.NormalizeRoot(executablePath);
        var components = DirectoryComponents(Path.GetDirectoryName(target)!);

        var issue = CheckAttributes(components, target, root, cancellationToken);
        if (issue is not null)
            return Task.FromResult(new ExecutableRevisionResult(null, issue));

        cancellationToken.ThrowIfCancellationRequested();
        FileRevision revision;
        try
        {
            revision = _readRevision(target);
        }
        catch (Exception error) when (IsExpectedIoError(error))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Failure(target, IssueFor(error, false)));
        }

        cancellationToken.ThrowIfCancellationRequested();
        issue = CheckAttributes(components, target, root, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(issue is null
            ? new ExecutableRevisionResult(revision, null)
            : new ExecutableRevisionResult(null, issue));
    }

    private InventoryIssue? CheckAttributes(
        IReadOnlyList<string> directories, string target, string root,
        CancellationToken cancellationToken)
    {
        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileAttributes attributes;
            try
            {
                attributes = _readAttributes(directory);
            }
            catch (Exception error) when (IsExpectedIoError(error))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new InventoryIssue(directory,
                    IssueFor(error, directory.Length <= root.Length));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return new InventoryIssue(directory, InventoryIssueKind.ReparsePoint);
            if ((attributes & FileAttributes.Directory) == 0)
                return new InventoryIssue(directory, InventoryIssueKind.InvalidRoot);
        }

        cancellationToken.ThrowIfCancellationRequested();
        FileAttributes fileAttributes;
        try
        {
            fileAttributes = _readAttributes(target);
        }
        catch (Exception error) when (IsExpectedIoError(error))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new InventoryIssue(target, IssueFor(error, false));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if ((fileAttributes & FileAttributes.ReparsePoint) != 0)
            return new InventoryIssue(target, InventoryIssueKind.ReparsePoint);
        if ((fileAttributes & FileAttributes.Directory) != 0)
            return new InventoryIssue(target, InventoryIssueKind.InvalidRoot);
        return null;
    }

    private static IReadOnlyList<string> DirectoryComponents(string directory)
    {
        var volume = Path.GetPathRoot(directory)!;
        var components = new List<string> { volume };
        var current = volume;
        foreach (var part in directory[volume.Length..].Split('\\',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            components.Add(current);
        }
        return components;
    }

    private static ExecutableRevisionResult Failure(string path, InventoryIssueKind kind) =>
        new(null, new InventoryIssue(path, kind));

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
