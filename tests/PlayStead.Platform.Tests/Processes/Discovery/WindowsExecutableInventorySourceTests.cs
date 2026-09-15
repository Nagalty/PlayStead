using PlayStead.Core.Library;
using PlayStead.Core.Sessions.Discovery;
using PlayStead.Platform.Processes.Discovery;

namespace PlayStead.Platform.Tests.Processes.Discovery;

public sealed class WindowsExecutableInventorySourceTests
{
    [Fact]
    public async Task Root_executable_preserves_scope_and_generation()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var path = directory.Write("game.exe", [1, 2, 3]);
        var scope = Scope(directory.Root);

        var result = await new WindowsExecutableInventorySource()
            .InventoryAsync(scope, CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Complete, result.Completeness);
        Assert.Equal(scope.GameId, result.Scope.GameId);
        Assert.Equal(scope.InstallationId, result.Scope.InstallationId);
        Assert.Equal(scope.GenerationId, result.Scope.GenerationId);
        Assert.True(result.Scope.IsPresent);
        Assert.Equal(path, Assert.Single(result.Candidates).ExecutablePath);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task Recursive_and_case_insensitive_extension_filters_other_files()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var exe = directory.Write(@"bin\Game.EXE", [4]);
        directory.Write(@"bin\readme.txt", [2]);

        var result = await new WindowsExecutableInventorySource()
            .InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Complete, result.Completeness);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(exe, candidate.ExecutablePath);
        Assert.Equal("Game.EXE", candidate.ExecutableName);
    }

    [Fact]
    public async Task Empty_is_complete()
    {
        using var directory = new ExecutableInventoryTestDirectory();

        var result = await new WindowsExecutableInventorySource()
            .InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Complete, result.Completeness);
        Assert.Empty(result.Candidates);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task Missing_root_is_incomplete_and_is_not_created()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var missing = Path.Combine(directory.Root, "missing");

        var result = await new WindowsExecutableInventorySource()
            .InventoryAsync(Scope(missing), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Empty(result.Candidates);
        Assert.Equal(InventoryIssueKind.MissingRoot, Assert.Single(result.Issues).Kind);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public async Task File_denied_keeps_other_candidates()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var denied = directory.Write("denied.exe", [1]);
        var good = directory.Write("good.exe", [2]);
        var source = Source(readRevision: path =>
            path == denied ? throw new UnauthorizedAccessException() : Revision(path));

        var result = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Equal(good, Assert.Single(result.Candidates).ExecutablePath);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(denied, issue.Path);
        Assert.Equal(InventoryIssueKind.AccessDenied, issue.Kind);
    }

    [Fact]
    public async Task Subtree_denied_keeps_other_branches()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var good = directory.Write(@"open\good.exe", [1]);
        directory.Write(@"closed\hidden.exe", [2]);
        var closed = Path.Combine(directory.Root, "closed");
        var source = Source(enumerateEntries: path =>
            path == closed ? throw new UnauthorizedAccessException() : Directory.EnumerateFileSystemEntries(path));

        var result = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Equal(good, Assert.Single(result.Candidates).ExecutablePath);
        Assert.Contains(result.Issues, issue => issue.Path == closed && issue.Kind == InventoryIssueKind.AccessDenied);
    }

    [Fact]
    public async Task Deferred_enumeration_error_keeps_yielded_candidate()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var good = directory.Write("good.exe", [1]);
        var source = Source(enumerateEntries: path =>
            path == directory.Root ? YieldThenFail(good) : Directory.EnumerateFileSystemEntries(path));

        var result = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Equal(good, Assert.Single(result.Candidates).ExecutablePath);
        Assert.Equal(InventoryIssueKind.IoFailure, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public async Task Root_denied_is_incomplete()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var source = Source(readAttributes: path =>
            path == directory.Root ? throw new UnauthorizedAccessException() : File.GetAttributes(path));

        var result = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Empty(result.Candidates);
        Assert.Equal(InventoryIssueKind.AccessDenied, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public async Task Canonical_result_normalizes_root_and_executable_paths()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var exe = directory.Write(@"bin\game.exe", [1]);
        var scope = Scope(directory.Root.Replace('\\', '/') + "/");

        var result = await new WindowsExecutableInventorySource()
            .InventoryAsync(scope, CancellationToken.None);

        Assert.Equal(directory.Root, result.Scope.RootPath);
        Assert.Equal(scope.GenerationId, result.Scope.GenerationId);
        Assert.Equal(exe, Assert.Single(result.Candidates).ExecutablePath);
    }

    [Fact]
    public async Task Sibling_prefix_entry_is_rejected()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var foo = Path.Combine(directory.Root, "Foo");
        Directory.CreateDirectory(foo);
        var outside = directory.Write(@"FooBar\game.exe", [1]);
        var source = Source(enumerateEntries: _ => [outside]);

        var result = await source.InventoryAsync(Scope(foo), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Empty(result.Candidates);
        Assert.Equal(InventoryIssueKind.EscapedRoot, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public async Task Traversal_entry_is_rejected_before_file_access()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var foo = Path.Combine(directory.Root, "Foo");
        Directory.CreateDirectory(foo);
        directory.Write(@"Other\game.exe", [1]);
        var traversal = Path.Combine(foo, "..", "Other", "game.exe");
        var source = Source(enumerateEntries: _ => [traversal]);

        var result = await source.InventoryAsync(Scope(foo), CancellationToken.None);

        Assert.Empty(result.Candidates);
        Assert.Equal(InventoryIssueKind.EscapedRoot, Assert.Single(result.Issues).Kind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Injected_reparse_file_or_directory_is_not_followed(bool directoryLink)
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var target = directoryLink
            ? Path.Combine(directory.Root, "linked")
            : directory.Write("linked.exe", [1]);
        if (directoryLink)
            Directory.CreateDirectory(target);
        var accessedBelowLink = false;
        var source = Source(
            enumerateEntries: path =>
            {
                if (path == target) accessedBelowLink = true;
                return Directory.EnumerateFileSystemEntries(path);
            },
            readAttributes: path =>
                path == target ? FileAttributes.ReparsePoint | (directoryLink ? FileAttributes.Directory : 0)
                    : File.GetAttributes(path),
            readRevision: path =>
            {
                if (path == target) accessedBelowLink = true;
                return Revision(path);
            });

        var result = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Empty(result.Candidates);
        Assert.False(accessedBelowLink);
        Assert.Equal(InventoryIssueKind.ReparsePoint, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public async Task Reparse_ancestor_of_root_is_rejected_before_enumeration()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var ancestor = Path.GetDirectoryName(directory.Root)!;
        var enumerated = false;
        var source = Source(
            enumerateEntries: path =>
            {
                enumerated = true;
                return Directory.EnumerateFileSystemEntries(path);
            },
            readAttributes: path =>
                path == ancestor ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : File.GetAttributes(path));

        var result = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.False(enumerated);
        Assert.Equal(InventoryIssueKind.ReparsePoint, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public async Task Real_reparse_directory_does_not_leak_sibling_target_contents()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        directory.Write(@"target\hidden.exe", [1]);
        var scanned = Path.Combine(directory.Root, "scanned");
        Directory.CreateDirectory(scanned);
        var link = directory.CreateDirectoryLink(@"scanned\link", "target");

        var result = await new WindowsExecutableInventorySource()
            .InventoryAsync(Scope(scanned), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Empty(result.Candidates);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(link, issue.Path);
        Assert.Equal(InventoryIssueKind.ReparsePoint, issue.Kind);
    }

    [Fact]
    public async Task Nested_enumerator_entry_cannot_bypass_a_reparse_parent()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        directory.Write(@"target\hidden.exe", [1]);
        var scanned = Path.Combine(directory.Root, "scanned");
        Directory.CreateDirectory(scanned);
        var link = directory.CreateDirectoryLink(@"scanned\link", "target");
        var nested = Path.Combine(link, "hidden.exe");
        var enumeratedThroughLink = false;
        var revisedThroughLink = false;
        var source = Source(
            enumerateEntries: path =>
            {
                if (path == link) enumeratedThroughLink = true;
                return path == scanned ? [nested] : [];
            },
            readRevision: path =>
            {
                if (path == nested) revisedThroughLink = true;
                return Revision(path);
            });

        var result = await source.InventoryAsync(Scope(scanned), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Empty(result.Candidates);
        Assert.False(enumeratedThroughLink);
        Assert.False(revisedThroughLink);
        Assert.Equal(InventoryIssueKind.EscapedRoot, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public async Task Queued_directory_changed_to_reparse_is_not_traversed()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var hidden = directory.Write(@"queued\hidden.exe", [1]);
        var queued = Path.GetDirectoryName(hidden)!;
        var queuedAttributeReads = 0;
        var enumeratedThroughLink = false;
        var revisedThroughLink = false;
        var source = Source(
            enumerateEntries: path =>
            {
                if (path == queued) enumeratedThroughLink = true;
                return Directory.EnumerateFileSystemEntries(path);
            },
            readAttributes: path =>
            {
                if (path == queued && ++queuedAttributeReads >= 2)
                    return FileAttributes.Directory | FileAttributes.ReparsePoint;
                return File.GetAttributes(path);
            },
            readRevision: path =>
            {
                if (path == hidden) revisedThroughLink = true;
                return Revision(path);
            });

        var result = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Empty(result.Candidates);
        Assert.False(enumeratedThroughLink);
        Assert.False(revisedThroughLink);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(queued, issue.Path);
        Assert.Equal(InventoryIssueKind.ReparsePoint, issue.Kind);
    }

    [Fact]
    public async Task Queued_directory_rechecks_its_root_ancestor_before_traversal()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var hidden = directory.Write(@"queued\hidden.exe", [1]);
        var queued = Path.GetDirectoryName(hidden)!;
        var rootAttributeReads = 0;
        var enumeratedQueued = false;
        var revisedQueued = false;
        var source = Source(
            enumerateEntries: path =>
            {
                if (path == queued) enumeratedQueued = true;
                return Directory.EnumerateFileSystemEntries(path);
            },
            readAttributes: path =>
            {
                if (path == directory.Root && ++rootAttributeReads >= 3)
                    return FileAttributes.Directory | FileAttributes.ReparsePoint;
                return File.GetAttributes(path);
            },
            readRevision: path =>
            {
                if (path == hidden) revisedQueued = true;
                return Revision(path);
            });

        var result = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Empty(result.Candidates);
        Assert.False(enumeratedQueued);
        Assert.False(revisedQueued);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(directory.Root, issue.Path);
        Assert.Equal(InventoryIssueKind.ReparsePoint, issue.Kind);
    }

    [Fact]
    public async Task Cancel_before_access_calls_no_filesystem_delegate()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var calls = 0;
        var source = new WindowsExecutableInventorySource(
            _ => { calls++; return []; },
            _ => { calls++; return FileAttributes.Directory; },
            _ => { calls++; return new FileRevision(1, DateTimeOffset.UtcNow); });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.InventoryAsync(Scope(directory.Root), cancellation.Token));

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Cancel_during_enumeration_returns_no_partial_inventory()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var good = directory.Write("good.exe", [1]);
        using var cancellation = new CancellationTokenSource();
        var source = Source(enumerateEntries: path =>
            path == directory.Root ? YieldThenCancel(good, cancellation) : Directory.EnumerateFileSystemEntries(path));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.InventoryAsync(Scope(directory.Root), cancellation.Token));
    }

    [Fact]
    public async Task Revision_values_are_read_from_file_without_executing_it()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var exe = directory.Write("game.exe", [1, 2, 3, 4]);
        var stamp = new DateTime(2024, 4, 5, 6, 7, 8, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(exe, stamp);

        var result = await new WindowsExecutableInventorySource()
            .InventoryAsync(Scope(directory.Root), CancellationToken.None);

        var revision = Assert.Single(result.Candidates).Revision;
        Assert.Equal(4, revision.SizeBytes);
        Assert.Equal(new DateTimeOffset(stamp), revision.LastWriteTimeUtc);
        Assert.Equal(TimeSpan.Zero, revision.LastWriteTimeUtc.Offset);
    }

    [Fact]
    public async Task Revision_changes_between_inventory_calls_without_changing_generation()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var exe = directory.Write("game.exe", [1]);
        var scope = Scope(directory.Root);
        var source = new WindowsExecutableInventorySource();

        var first = await source.InventoryAsync(scope, CancellationToken.None);
        File.WriteAllBytes(exe, [1, 2]);
        File.SetLastWriteTimeUtc(exe, new DateTime(2024, 4, 5, 6, 7, 8, DateTimeKind.Utc));
        var second = await source.InventoryAsync(scope, CancellationToken.None);

        Assert.NotEqual(Assert.Single(first.Candidates).Revision,
            Assert.Single(second.Candidates).Revision);
        Assert.Equal(scope.GenerationId, first.Scope.GenerationId);
        Assert.Equal(scope.GenerationId, second.Scope.GenerationId);
    }

    [Fact]
    public async Task Deterministic_order_sorts_candidates_and_issues()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var alpha = directory.Write("Alpha.exe", [1]);
        var beta = directory.Write("beta.EXE", [1]);
        var badA = directory.Write("A-bad.exe", [1]);
        var badB = directory.Write("z-bad.exe", [1]);
        var files = new[] { badB, beta, badA, alpha };
        WindowsExecutableInventorySource Create(IEnumerable<string> order) => Source(
            enumerateEntries: path => path == directory.Root ? order : [],
            readRevision: path => path == badA || path == badB
                ? throw new IOException("Injected revision error.") : Revision(path));

        var first = await Create(files).InventoryAsync(Scope(directory.Root), CancellationToken.None);
        var second = await Create(files.Reverse()).InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(new[] { alpha, beta }, first.Candidates.Select(candidate => candidate.ExecutablePath));
        Assert.Equal(new[] { badA, badB }, first.Issues.Select(issue => issue.Path));
        Assert.Equal(first.Candidates.Select(candidate => candidate.ExecutablePath),
            second.Candidates.Select(candidate => candidate.ExecutablePath));
        Assert.Equal(first.Issues, second.Issues);
    }

    [Fact]
    public async Task Duplicate_entry_is_an_issue_and_not_a_second_candidate()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var exe = directory.Write("game.exe", [1]);
        var source = Source(enumerateEntries: _ => [exe, exe]);

        var result = await source.InventoryAsync(Scope(directory.Root), CancellationToken.None);

        Assert.Equal(InventoryCompleteness.Incomplete, result.Completeness);
        Assert.Equal(exe, Assert.Single(result.Candidates).ExecutablePath);
        Assert.Equal(InventoryIssueKind.InvalidRoot, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public async Task Unexpected_programming_exception_propagates()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var source = Source(enumerateEntries: _ => throw new InvalidOperationException("Bug."));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            source.InventoryAsync(Scope(directory.Root), CancellationToken.None));
    }

    [Fact]
    public void Delegate_constructor_rejects_null_inputs()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WindowsExecutableInventorySource(null!, File.GetAttributes, Revision));
        Assert.Throws<ArgumentNullException>(() =>
            new WindowsExecutableInventorySource(Directory.EnumerateFileSystemEntries, null!, Revision));
        Assert.Throws<ArgumentNullException>(() =>
            new WindowsExecutableInventorySource(Directory.EnumerateFileSystemEntries, File.GetAttributes, null!));
    }

    private static InstallationScope Scope(string root) =>
        new(GameId.New(), InstallationId.New(), root, Guid.NewGuid(), true);

    private static WindowsExecutableInventorySource Source(
        Func<string, IEnumerable<string>>? enumerateEntries = null,
        Func<string, FileAttributes>? readAttributes = null,
        Func<string, FileRevision>? readRevision = null) =>
        new(enumerateEntries ?? Directory.EnumerateFileSystemEntries,
            readAttributes ?? File.GetAttributes, readRevision ?? Revision);

    private static FileRevision Revision(string path)
    {
        var info = new FileInfo(path);
        return new FileRevision(info.Length, new DateTimeOffset(info.LastWriteTimeUtc));
    }

    private static IEnumerable<string> YieldThenFail(string path)
    {
        yield return path;
        throw new IOException("Injected deferred enumeration error.");
    }

    private static IEnumerable<string> YieldThenCancel(string path, CancellationTokenSource cancellation)
    {
        yield return path;
        cancellation.Cancel();
    }
}
