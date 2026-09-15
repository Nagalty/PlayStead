using PlayStead.Platform.Processes.Discovery;

namespace PlayStead.Platform.Tests.Processes.Discovery;

internal sealed class ExecutableInventoryTestDirectory : IDisposable
{
    private readonly List<string> _directoryLinks = [];
    private readonly string _discoveryParent;

    public ExecutableInventoryTestDirectory()
    {
        _discoveryParent = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(), "PlayStead.Tests", "Discovery"));
        Root = Path.GetFullPath(Path.Combine(
            _discoveryParent, Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Write(string relativePath, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var path = ResolveUnderRoot(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public string CreateDirectoryLink(string relativeLink, string relativeTarget)
    {
        var link = ResolveUnderRoot(relativeLink);
        var target = ResolveUnderRoot(relativeTarget);
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        Directory.CreateSymbolicLink(link, target);
        _directoryLinks.Add(link);
        return link;
    }

    public void Dispose()
    {
        if (!WindowsExecutablePath.IsStrictlyUnderRoot(_discoveryParent, Root))
            throw new InvalidOperationException("Fixture root escaped the Discovery temp directory.");

        foreach (var link in _directoryLinks)
        {
            if (!WindowsExecutablePath.IsStrictlyUnderRoot(Root, link))
                throw new InvalidOperationException("Fixture link escaped its root.");
            if (Directory.Exists(link))
                Directory.Delete(link, recursive: false);
        }

        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }

    private string ResolveUnderRoot(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var path = Path.GetFullPath(Path.Combine(Root, relativePath));
        if (!WindowsExecutablePath.IsStrictlyUnderRoot(Root, path))
            throw new ArgumentException("Fixture path must stay under its root.", nameof(relativePath));
        return path;
    }
}
