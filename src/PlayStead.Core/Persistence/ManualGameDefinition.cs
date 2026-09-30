using PlayStead.Core.Library;

namespace PlayStead.Core.Persistence;

public sealed record ManualGameDefinition(
    string Title,
    string ExecutablePath,
    string WorkingDirectory,
    string? LaunchArguments = null)
{
    public static ManualGameDefinition Create(
        string title,
        string executablePath,
        string? workingDirectory,
        string? launchArguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        if (!Path.IsPathFullyQualified(executablePath))
            throw new ArgumentException("The executable path must be absolute.", nameof(executablePath));

        var executable = Path.GetFullPath(executablePath);
        if (!File.Exists(executable))
            throw new FileNotFoundException("The executable does not exist.", executable);

        if (File.GetAttributes(executable).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("The executable is a reparse point.");

        var extension = Path.GetExtension(executable);
        if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".com", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The executable extension is not supported.", nameof(executablePath));

        if (workingDirectory is not null && !Path.IsPathFullyQualified(workingDirectory))
            throw new ArgumentException("The working directory must be absolute.", nameof(workingDirectory));

        var directory = Path.GetFullPath(workingDirectory ?? Path.GetDirectoryName(executable)!);
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(directory);

        if (new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("The working directory is a reparse point.");

        return new ManualGameDefinition(
            title.Trim(),
            executable,
            directory,
            string.IsNullOrWhiteSpace(launchArguments) ? null : launchArguments);
    }
}

public interface IManualGameStore
{
    Task<GameInstallation> CreateAsync(
        ManualGameDefinition definition,
        CancellationToken cancellationToken);

    Task<GameInstallation?> UpdateAsync(
        GameId gameId,
        ManualGameDefinition definition,
        CancellationToken cancellationToken);

    Task<bool> RemoveAsync(
        GameId gameId,
        CancellationToken cancellationToken);
}
