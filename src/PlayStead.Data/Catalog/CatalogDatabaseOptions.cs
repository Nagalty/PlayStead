namespace PlayStead.Data.Catalog;

public sealed record CatalogDatabaseOptions(
    string CatalogPath,
    string BackupsDirectory);
