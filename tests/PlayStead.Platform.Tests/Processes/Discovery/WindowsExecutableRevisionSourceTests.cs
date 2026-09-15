using PlayStead.Core.Sessions.Discovery;
using PlayStead.Core.Library;
using PlayStead.Platform.Processes.Discovery;

namespace PlayStead.Platform.Tests.Processes.Discovery;

public sealed class WindowsExecutableRevisionSourceTests
{
    [Fact]
    public void Targeted_revision_reader_contract_exists()
    {
        var assembly = typeof(InstallationScope).Assembly;
        Assert.NotNull(assembly.GetType("PlayStead.Core.Sessions.Discovery.ExecutableRevisionResult"));
        Assert.NotNull(assembly.GetType("PlayStead.Core.Sessions.Discovery.IExecutableRevisionSource"));
        Assert.NotNull(Type.GetType(
            "PlayStead.Platform.Processes.Discovery.WindowsExecutableRevisionSource, PlayStead.Platform"));
    }

    [Fact]
    public void Result_rejects_missing_or_ambiguous_outcome()
    {
        var revision = new FileRevision(1, DateTimeOffset.UnixEpoch);
        var issue = new InventoryIssue(@"C:\Game\game.exe", InventoryIssueKind.IoFailure);
        Assert.Throws<ArgumentException>(() => new ExecutableRevisionResult(null, null));
        Assert.Throws<ArgumentException>(() => new ExecutableRevisionResult(revision, issue));
    }

    [Fact]
    public async Task Targeted_read_returns_size_and_utc_timestamp()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var path = directory.Write("game.exe", [1, 2, 3, 4]);
        var timestamp = new DateTime(2024, 4, 5, 6, 7, 8, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, timestamp);

        var result = await new WindowsExecutableRevisionSource()
            .ReadAsync(Scope(directory.Root), path, CancellationToken.None);

        Assert.Equal(4, result.Revision?.SizeBytes);
        Assert.Equal(new DateTimeOffset(timestamp), result.Revision?.LastWriteTimeUtc);
        Assert.Null(result.Issue);
    }

    [Fact]
    public async Task Changed_file_returns_new_revision()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var path = directory.Write("game.exe", [1]);
        var reader = new WindowsExecutableRevisionSource();
        var before = await reader.ReadAsync(Scope(directory.Root), path, CancellationToken.None);
        File.WriteAllBytes(path, [1, 2, 3]);

        var after = await reader.ReadAsync(Scope(directory.Root), path, CancellationToken.None);

        Assert.Equal(1, before.Revision?.SizeBytes);
        Assert.Equal(3, after.Revision?.SizeBytes);
        Assert.Null(after.Issue);
    }

    [Fact]
    public async Task Missing_file_is_explicit_failure()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var path = Path.Combine(directory.Root, "missing.exe");
        var result = await new WindowsExecutableRevisionSource()
            .ReadAsync(Scope(directory.Root), path, CancellationToken.None);
        Assert.Null(result.Revision);
        Assert.Equal(path, result.Issue?.Path);
        Assert.Equal(InventoryIssueKind.IoFailure, result.Issue?.Kind);
    }

    [Fact]
    public async Task Missing_scope_root_is_explicit_failure()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var root = Path.Combine(directory.Root, "absent");
        var result = await new WindowsExecutableRevisionSource()
            .ReadAsync(Scope(root), Path.Combine(root, "game.exe"), CancellationToken.None);
        Assert.Null(result.Revision);
        Assert.Equal(InventoryIssueKind.MissingRoot, result.Issue?.Kind);
    }

    [Fact]
    public async Task Access_denied_is_explicit_failure()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var path = directory.Write("game.exe", [1]);
        var reader = Source(revision: _ => throw new UnauthorizedAccessException());
        var result = await reader.ReadAsync(Scope(directory.Root), path, CancellationToken.None);
        Assert.Null(result.Revision);
        Assert.Equal(InventoryIssueKind.AccessDenied, result.Issue?.Kind);
    }

    [Fact]
    public async Task Io_failure_is_explicit_failure()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var path = directory.Write("game.exe", [1]);
        var reader = Source(revision: _ => throw new IOException());
        var result = await reader.ReadAsync(Scope(directory.Root), path, CancellationToken.None);
        Assert.Null(result.Revision);
        Assert.Equal(InventoryIssueKind.IoFailure, result.Issue?.Kind);
    }

    [Fact]
    public async Task Root_boundary_rejects_sibling_prefix()
    {
        using var directory = new ExecutableInventoryTestDirectory();
        var calls = 0;
        var reader = new WindowsExecutableRevisionSource(
            _ => { calls++; return FileAttributes.Directory; },
            _ => { calls++; return Revision(1); });
        var result = await reader.ReadAsync(Scope(directory.Root),
            directory.Root + "Sibling\\game.exe", CancellationToken.None);
        Assert.Equal(InventoryIssueKind.EscapedRoot, result.Issue?.Kind);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("game.exe")]
    [InlineData(@"C:\Games\Example\..\Other\game.exe")]
    public async Task Relative_or_traversal_path_is_rejected(string path)
    {
        var result = await new WindowsExecutableRevisionSource(
            _ => throw new InvalidOperationException("unexpected I/O"),
            _ => throw new InvalidOperationException("unexpected I/O"))
            .ReadAsync(Scope(@"C:\Games\Example"), path, CancellationToken.None);
        Assert.Null(result.Revision);
        Assert.Equal(InventoryIssueKind.EscapedRoot, result.Issue?.Kind);
    }

    [Fact]
    public async Task File_reparse_point_is_rejected()
    {
        var target = @"C:\Games\Example\game.exe";
        var reader = SyntheticSource(path => path == target
            ? FileAttributes.ReparsePoint : FileAttributes.Directory);
        var result = await reader.ReadAsync(Scope(@"C:\Games\Example"), target, CancellationToken.None);
        Assert.Equal(InventoryIssueKind.ReparsePoint, result.Issue?.Kind);
    }

    [Fact]
    public async Task Ancestor_reparse_point_is_rejected()
    {
        var target = @"C:\Games\Example\bin\game.exe";
        var reader = SyntheticSource(path => path == @"C:\Games\Example\bin"
            ? FileAttributes.Directory | FileAttributes.ReparsePoint
            : path == target ? FileAttributes.Normal : FileAttributes.Directory);
        var result = await reader.ReadAsync(Scope(@"C:\Games\Example"), target, CancellationToken.None);
        Assert.Equal(InventoryIssueKind.ReparsePoint, result.Issue?.Kind);
    }

    [Fact]
    public async Task Reparse_point_appearing_during_read_is_rejected()
    {
        var target = @"C:\Games\Example\game.exe";
        var read = false;
        var reader = new WindowsExecutableRevisionSource(
            path => path == target
                ? read ? FileAttributes.ReparsePoint : FileAttributes.Normal
                : FileAttributes.Directory,
            _ => { read = true; return Revision(12); });
        var result = await reader.ReadAsync(Scope(@"C:\Games\Example"), target, CancellationToken.None);
        Assert.Null(result.Revision);
        Assert.Equal(InventoryIssueKind.ReparsePoint, result.Issue?.Kind);
    }

    [Fact]
    public async Task Ancestor_reparse_point_appearing_during_read_is_rejected()
    {
        var target = @"C:\Games\Example\game.exe";
        var read = false;
        var reader = new WindowsExecutableRevisionSource(
            path => path == @"C:\Games\Example" && read
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : path == target ? FileAttributes.Normal : FileAttributes.Directory,
            _ => { read = true; return Revision(12); });
        var result = await reader.ReadAsync(Scope(@"C:\Games\Example"), target, CancellationToken.None);
        Assert.Null(result.Revision);
        Assert.Equal(InventoryIssueKind.ReparsePoint, result.Issue?.Kind);
    }

    [Fact]
    public async Task Absent_scope_performs_no_file_read()
    {
        var calls = 0;
        var reader = new WindowsExecutableRevisionSource(
            _ => { calls++; return FileAttributes.Directory; },
            _ => { calls++; return Revision(1); });
        var scope = Scope(@"C:\Games\Example", present: false);
        var result = await reader.ReadAsync(scope, @"C:\Games\Example\game.exe", CancellationToken.None);
        Assert.Equal(InventoryIssueKind.MissingRoot, result.Issue?.Kind);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Cancellation_before_or_during_read_propagates()
    {
        var target = @"C:\Games\Example\game.exe";
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reader = SyntheticSource(path => path == target
            ? FileAttributes.Normal : FileAttributes.Directory);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.ReadAsync(Scope(@"C:\Games\Example"), target, cancellation.Token));

        using var during = new CancellationTokenSource();
        var reads = 0;
        reader = new WindowsExecutableRevisionSource(
            path => path == target ? FileAttributes.Normal : FileAttributes.Directory,
            _ => { reads++; during.Cancel(); return Revision(1); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.ReadAsync(Scope(@"C:\Games\Example"), target, during.Token));
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task Cancellation_between_attribute_reads_propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        var reader = new WindowsExecutableRevisionSource(
            _ => { reads++; cancellation.Cancel(); return FileAttributes.Directory; },
            _ => throw new InvalidOperationException("revision should not be read"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.ReadAsync(Scope(@"C:\Games\Example"),
                @"C:\Games\Example\game.exe", cancellation.Token));
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task Cancellation_during_directory_attribute_failure_propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var reader = new WindowsExecutableRevisionSource(
            _ => { cancellation.Cancel(); throw new IOException("directory read failed"); },
            _ => throw new InvalidOperationException("revision should not be read"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.ReadAsync(Scope(@"C:\Games\Example"),
                @"C:\Games\Example\game.exe", cancellation.Token));
    }

    [Fact]
    public async Task Cancellation_during_target_attribute_failure_propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var target = @"C:\Games\Example\game.exe";
        var reader = new WindowsExecutableRevisionSource(
            path => path == target
                ? throw CancelThenDeny(cancellation)
                : FileAttributes.Directory,
            _ => throw new InvalidOperationException("revision should not be read"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.ReadAsync(Scope(@"C:\Games\Example"), target, cancellation.Token));
    }

    [Fact]
    public async Task Cancellation_during_revision_failure_propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var target = @"C:\Games\Example\game.exe";
        var reader = new WindowsExecutableRevisionSource(
            path => path == target ? FileAttributes.Normal : FileAttributes.Directory,
            _ => { cancellation.Cancel(); throw new IOException("revision read failed"); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            reader.ReadAsync(Scope(@"C:\Games\Example"), target, cancellation.Token));
    }

    [Fact]
    public async Task Reader_touches_only_target_and_ancestor_attributes()
    {
        var target = @"C:\Games\Example\Game.exe";
        var attributes = new List<string>();
        var revisions = new List<string>();
        var revision = Revision(12);
        var reader = new WindowsExecutableRevisionSource(
            path => { attributes.Add(path); return path == target
                ? FileAttributes.Normal : FileAttributes.Directory; },
            path => { revisions.Add(path); return revision; });
        var result = await reader.ReadAsync(Scope(@"C:\Games\Example"), target, CancellationToken.None);
        Assert.Equal(revision, result.Revision);
        Assert.Null(result.Issue);
        Assert.Equal(new[] { target }, revisions);
        Assert.Equal(new[] { @"C:\", @"C:\Games", @"C:\Games\Example", target,
            @"C:\", @"C:\Games", @"C:\Games\Example", target }, attributes);
    }

    [Fact]
    public async Task Directory_in_place_of_executable_is_rejected()
    {
        var target = @"C:\Games\Example\game.exe";
        var result = await SyntheticSource(_ => FileAttributes.Directory)
            .ReadAsync(Scope(@"C:\Games\Example"), target, CancellationToken.None);
        Assert.Null(result.Revision);
        Assert.Equal(InventoryIssueKind.InvalidRoot, result.Issue?.Kind);
    }

    [Fact]
    public async Task Programming_error_propagates()
    {
        var target = @"C:\Games\Example\game.exe";
        var reader = new WindowsExecutableRevisionSource(
            path => path == target ? FileAttributes.Normal : FileAttributes.Directory,
            _ => throw new InvalidOperationException("bug"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reader.ReadAsync(Scope(@"C:\Games\Example"), target, CancellationToken.None));
    }

    private static InstallationScope Scope(string root, bool present = true) =>
        new(GameId.New(), InstallationId.New(), root, Guid.NewGuid(), present);

    private static FileRevision Revision(long bytes) =>
        new(bytes, DateTimeOffset.UnixEpoch);

    private static UnauthorizedAccessException CancelThenDeny(CancellationTokenSource source)
    {
        source.Cancel();
        return new UnauthorizedAccessException("target read denied");
    }

    private static WindowsExecutableRevisionSource Source(
        Func<string, FileRevision>? revision = null) =>
        new(File.GetAttributes, revision ?? (_ => Revision(1)));

    private static WindowsExecutableRevisionSource SyntheticSource(
        Func<string, FileAttributes> attributes) =>
        new(attributes, _ => Revision(1));
}
