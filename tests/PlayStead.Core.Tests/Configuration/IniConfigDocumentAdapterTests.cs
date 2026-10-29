using PlayStead.Core.Configuration;

namespace PlayStead.Core.Tests.Configuration;

public sealed class IniConfigDocumentAdapterTests
{
    [Fact]
    public void Restore_preserves_comments_blank_lines_and_unselected_sections()
    {
        var adapter = new IniConfigDocumentAdapter();
        var current = adapter.Parse("x.ini", "; comment\r\n[G]\r\nx=2\r\n\r\n[New]\r\ny=3\r\n");
        var reference = adapter.Parse("x.ini", "[G]\nx=1\n");
        var change = Assert.Single(adapter.Compare(reference, current), x => x.CanRestore);
        Assert.Equal("; comment\r\n[G]\r\nx=1\r\n\r\n[New]\r\ny=3\r\n", adapter.Serialize(adapter.ApplyChanges(current, [change])));
    }
    [Fact]
    public void Compare_uses_section_and_key_and_ignores_order()
    {
        var adapter = new IniConfigDocumentAdapter();
        var reference = adapter.Parse("settings.ini", "[Graphics]\nVsync=true\nWidth=1920\n");
        var current = adapter.Parse("settings.ini", "[Graphics]\nWidth=1920\nVsync=false\nNewKey=1\n");
        var changes = adapter.Compare(reference, current);
        Assert.Contains(changes, x => x.TechnicalPath == "Graphics\u001fVsync" && x.ChangeKind == ConfigChangeKind.Modified && x.CanRestore);
        Assert.Contains(changes, x => x.ChangeKind == ConfigChangeKind.Added);
        Assert.DoesNotContain(changes, x => x.TechnicalPath.EndsWith("Width", StringComparison.Ordinal));
    }

    [Fact]
    public void Apply_changes_preserves_new_current_keys()
    {
        var adapter = new IniConfigDocumentAdapter();
        var reference = adapter.Parse("settings.ini", "[Graphics]\nVsync=true\n");
        var current = adapter.Parse("settings.ini", "[Graphics]\nVsync=false\nNewKey=1\n");
        var change = Assert.Single(adapter.Compare(reference, current), x => x.TechnicalPath.EndsWith("Vsync", StringComparison.Ordinal));
        var result = adapter.Serialize(adapter.ApplyChanges(current, [change]));
        Assert.Contains("Vsync=true", result);
        Assert.Contains("NewKey=1", result);
    }
}
