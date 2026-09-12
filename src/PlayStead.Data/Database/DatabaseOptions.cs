namespace PlayStead.Data.Database;

public sealed record DatabaseOptions(
    string DatabasePath,
    string BackupsDirectory);
