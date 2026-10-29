using PlayStead.Core.Configuration;

namespace PlayStead.Core.Tests.Configuration;

public sealed class JsonConfigDocumentAdapterTests
{
    [Fact]
    public void Compare_detects_nested_primitive_and_added_property()
    {
        var adapter = new JsonConfigDocumentAdapter();
        var reference = adapter.Parse("settings.json", "{\"graphics\":{\"vsync\":true,\"width\":1920}}");
        var current = adapter.Parse("settings.json", "{\"graphics\":{\"vsync\":false,\"width\":1920,\"newOption\":1}}");
        var changes = adapter.Compare(reference, current);
        Assert.Contains(changes, x => x.TechnicalPath == "graphics.vsync" && x.CanRestore);
        Assert.Contains(changes, x => x.TechnicalPath == "graphics.newOption" && x.ChangeKind == ConfigChangeKind.Added && !x.CanRestore);
    }

    [Fact]
    public void Ambiguous_array_is_not_restorable_and_invalid_json_does_not_parse()
    {
        var adapter = new JsonConfigDocumentAdapter();
        var reference = adapter.Parse("settings.json", "{\"mods\":[{\"id\":1}]}");
        var current = adapter.Parse("settings.json", "{\"mods\":[{\"id\":2}]}");
        Assert.False(Assert.Single(adapter.Compare(reference, current)).CanRestore);
        Assert.ThrowsAny<Exception>(() => adapter.Parse("settings.json", "{invalid"));
    }
}
