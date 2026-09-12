using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class WindowsSteamRootLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryLocate_returns_first_existing_candidate_as_full_path()
    {
        var missing = Path.Combine(_root, "Missing");
        var existing = Path.Combine(_root, "Steam");
        Directory.CreateDirectory(existing);

        var sut = new WindowsSteamRootLocator(
            new string?[]
            {
                null,
                " ",
                missing,
                existing,
                Path.Combine(_root, "Later")
            });

        var result = sut.TryLocate();

        Assert.Equal(Path.GetFullPath(existing), result);
    }

    [Fact]
    public void TryLocate_returns_null_when_no_candidate_exists()
    {
        var sut = new WindowsSteamRootLocator(
            new string?[]
            {
                null,
                " ",
                Path.Combine(_root, "MissingA"),
                Path.Combine(_root, "MissingB")
            });

        Assert.Null(sut.TryLocate());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
