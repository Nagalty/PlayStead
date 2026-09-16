namespace PlayStead.Core.Catalog;

public readonly record struct CatalogContentId(Guid Value)
{
    public static CatalogContentId New() => new(Guid.NewGuid());

    public override string ToString() =>
        Value.ToString("D");
}
