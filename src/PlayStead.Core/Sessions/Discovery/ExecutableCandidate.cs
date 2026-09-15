namespace PlayStead.Core.Sessions.Discovery;

public sealed record ExecutableCandidate
{
    public ExecutableCandidate(string executablePath, string executableName, FileRevision revision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
        ArgumentNullException.ThrowIfNull(revision);

        ExecutablePath = executablePath;
        ExecutableName = executableName;
        Revision = revision;
    }

    public string ExecutablePath { get; }
    public string ExecutableName { get; }
    public FileRevision Revision { get; }
}
