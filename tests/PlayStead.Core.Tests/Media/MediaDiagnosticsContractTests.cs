using PlayStead.Core.Media;

namespace PlayStead.Core.Tests.Media;

public sealed class MediaDiagnosticsContractTests
{
    [Fact]
    public void Media_diagnostics_contract_defines_all_plan_event_kinds()
    {
        var assembly = typeof(GameMediaResolver).Assembly;
        var eventType = assembly.GetType("PlayStead.Core.Media.MediaResolutionEvent");
        var diagnosticsType = assembly.GetType("PlayStead.Core.Media.IMediaDiagnostics");
        var kindType = assembly.GetType("PlayStead.Core.Media.MediaResolutionEventKind");

        Assert.NotNull(eventType);
        Assert.NotNull(diagnosticsType);
        Assert.NotNull(kindType);
        Assert.True(kindType!.IsEnum);
        Assert.Equal(
            new[]
            {
                "CacheHit",
                "CacheMiss",
                "LocalProviderHit",
                "RemoteProviderSuccess",
                "RemoteProviderFailure",
                "InvalidImage",
                "FallbackUsed"
            },
            Enum.GetNames(kindType));
    }
}
