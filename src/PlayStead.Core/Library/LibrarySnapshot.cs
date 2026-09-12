namespace PlayStead.Core.Library;

public sealed record LibrarySnapshot(
    IReadOnlyList<LogicalGame> Games,
    IReadOnlyList<GameInstallation> Installations);
