namespace PlayStead.Core.Catalog;

public enum CatalogRelationKind
{
    RequiresBaseGame = 1,
    StandaloneExpansionOf = 2,
    RemasterOf = 3,
    RemakeOf = 4,
    DemoOf = 5,
    PrologueOf = 6,
    TestClientOf = 7,
    DedicatedServerOf = 8,
    ToolFor = 9
}
