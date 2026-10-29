using PlayStead.Core.Configuration;

namespace PlayStead.Core.Tests.Configuration;

public sealed class ConfigSemanticModelTests
{
    [Fact]
    public void Change_model_keeps_old_and_current_values_and_restore_policy()
    {
        var change = new ConfigSemanticChange(
            "graphics.vsync",
            null,
            "true",
            "false",
            ConfigChangeKind.Modified,
            true,
            ConfigChangeConfidence.High,
            "graphics.vsync");

        Assert.Equal(ConfigChangeKind.Modified, change.ChangeKind);
        Assert.Equal("true", change.OldValue);
        Assert.Equal("false", change.CurrentValue);
        Assert.True(change.CanRestore);
        Assert.Equal(ConfigChangeConfidence.High, change.Confidence);
    }

    [Fact]
    public void Resolver_does_not_claim_unsupported_format()
    {
        var resolver = new ConfigDocumentAdapterResolver([
            new IniConfigDocumentAdapter(),
            new JsonConfigDocumentAdapter()
        ]);

        Assert.Null(resolver.Resolve("settings.toml", "key = value"));
    }
}
