using PlayStead.Providers.Steam.Remote;

namespace PlayStead.Providers.Tests.Steam.Remote;

[Collection("SteamCmdEnvironment")]
public sealed class SteamCmdPathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    private readonly string? _originalLocalAppData =
        Environment.GetEnvironmentVariable("LOCALAPPDATA");

    private readonly string? _originalPath =
        Environment.GetEnvironmentVariable("PATH");

    [Fact]
    public void Explicit_existing_path_wins_over_all_fallbacks()
    {
        Directory.CreateDirectory(_root);

        var explicitPath = Touch(
            Path.Combine(_root, "Explicit", "steamcmd.exe"));

        var localAppData = Path.Combine(_root, "LocalAppData");
        Touch(
            Path.Combine(
                localAppData,
                "PlayStead",
                "Tools",
                "SteamCMD",
                "steamcmd.exe"));

        var pathDirectory = Path.Combine(_root, "Path");
        Touch(Path.Combine(pathDirectory, "steamcmd.exe"));

        Environment.SetEnvironmentVariable(
            "LOCALAPPDATA",
            localAppData);

        Environment.SetEnvironmentVariable(
            "PATH",
            pathDirectory);

        var sut = new SteamCmdPathResolver(
            new SteamCmdOptions(
                explicitPath,
                TimeSpan.FromSeconds(30)));

        Assert.Equal(
            Path.GetFullPath(explicitPath),
            sut.Resolve());
    }

    [Fact]
    public void Falls_back_to_PlayStead_local_app_data_location()
    {
        Directory.CreateDirectory(_root);

        var localAppData = Path.Combine(_root, "LocalAppData");

        var expected = Touch(
            Path.Combine(
                localAppData,
                "PlayStead",
                "Tools",
                "SteamCMD",
                "steamcmd.exe"));

        Environment.SetEnvironmentVariable(
            "LOCALAPPDATA",
            localAppData);

        Environment.SetEnvironmentVariable(
            "PATH",
            string.Empty);

        var sut = new SteamCmdPathResolver(
            new SteamCmdOptions(
                ExecutablePath: null,
                QueryTimeout: TimeSpan.FromSeconds(30)));

        Assert.Equal(
            Path.GetFullPath(expected),
            sut.Resolve());
    }

    [Fact]
    public void Falls_back_to_steamcmd_on_PATH()
    {
        Directory.CreateDirectory(_root);

        var localAppData = Path.Combine(
            _root,
            "EmptyLocalAppData");

        Directory.CreateDirectory(localAppData);

        var pathDirectory = Path.Combine(
            _root,
            "Path");

        var expected = Touch(
            Path.Combine(
                pathDirectory,
                "steamcmd.exe"));

        Environment.SetEnvironmentVariable(
            "LOCALAPPDATA",
            localAppData);

        Environment.SetEnvironmentVariable(
            "PATH",
            pathDirectory);

        var sut = new SteamCmdPathResolver(
            new SteamCmdOptions(
                ExecutablePath: null,
                QueryTimeout: TimeSpan.FromSeconds(30)));

        Assert.Equal(
            Path.GetFullPath(expected),
            sut.Resolve());
    }

    [Fact]
    public void Returns_null_when_SteamCmd_cannot_be_found()
    {
        Directory.CreateDirectory(_root);

        var localAppData = Path.Combine(
            _root,
            "EmptyLocalAppData");

        var pathDirectory = Path.Combine(
            _root,
            "EmptyPath");

        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(pathDirectory);

        Environment.SetEnvironmentVariable(
            "LOCALAPPDATA",
            localAppData);

        Environment.SetEnvironmentVariable(
            "PATH",
            pathDirectory);

        var sut = new SteamCmdPathResolver(
            new SteamCmdOptions(
                ExecutablePath: null,
                QueryTimeout: TimeSpan.FromSeconds(30)));

        Assert.Null(sut.Resolve());
    }

    private static string Touch(string path)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(path)!);

        File.WriteAllText(path, string.Empty);

        return path;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(
            "LOCALAPPDATA",
            _originalLocalAppData);

        Environment.SetEnvironmentVariable(
            "PATH",
            _originalPath);

        if (Directory.Exists(_root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }
}
