using PlayStead.Core.Updates;

namespace PlayStead.Core.Tests.Updates;

public sealed class AppUpdateContractTests
{
    [Fact]
    public void GitHub_manifest_requires_https_package_and_sha256()
    {
        var valid = new GitHubUpdateManifest(
            "0.4.3",
            DistributionChannel.GitHub,
            new Uri("https://example.invalid/playstead.zip"),
            new string('a', 64));

        Assert.True(valid.IsValid(out var error), error);

        var invalid = valid with { PackageUri = new Uri("http://example.invalid/playstead.zip") };
        Assert.False(invalid.IsValid(out _));
    }

    [Fact]
    public void Unknown_state_has_no_action_or_uri()
    {
        var state = AppUpdateState.Unknown(DistributionChannel.GitHub, "0.4.1-dev");

        Assert.Equal(AppUpdateStatus.Unknown, state.Status);
        Assert.Equal(AppUpdateActionKind.None, state.Action);
        Assert.Null(state.ActionUri);
    }

    [Fact]
    public void Semantic_versions_order_prerelease_before_stable()
    {
        Assert.True(SemanticVersion.TryParse("0.4.3-alpha1", out var alpha1));
        Assert.True(SemanticVersion.TryParse("0.4.3-alpha2", out var alpha2));
        Assert.True(SemanticVersion.TryParse("0.4.3", out var stable));

        Assert.True(alpha1.CompareTo(alpha2) < 0);
        Assert.True(alpha2.CompareTo(stable) < 0);
    }
}
