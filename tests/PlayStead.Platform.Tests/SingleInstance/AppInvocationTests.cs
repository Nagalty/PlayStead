using System.Text;
using System.Text.Json;
using PlayStead.Platform.SingleInstance;

namespace PlayStead.Platform.Tests.SingleInstance;

public sealed class AppInvocationTests
{
    [Fact]
    public void Default_requests_activation_without_deep_link()
    {
        var invocation = AppInvocation.Default;

        Assert.True(invocation.Activate);
        Assert.Null(invocation.DeepLink);
    }

    [Fact]
    public void Utf8_json_round_trip_preserves_invocation()
    {
        var expected = new AppInvocation(
            Activate: true,
            DeepLink: "playstead://library/game/730");

        var utf8 = JsonSerializer.SerializeToUtf8Bytes(expected);

        var json = Encoding.UTF8.GetString(utf8);
        Assert.Contains("\"Activate\":true", json, StringComparison.Ordinal);
        Assert.Contains(
            "\"DeepLink\":\"playstead://library/game/730\"",
            json,
            StringComparison.Ordinal);

        var actual = JsonSerializer.Deserialize<AppInvocation>(utf8);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
    }
}
