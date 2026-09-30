using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Library;

public sealed class ManualGameDefinitionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-Manual-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Valid_definition_defaults_working_directory_and_preserves_arguments()
    {
        Directory.CreateDirectory(_root);
        var executable = Path.Combine(_root, "game.exe");
        File.WriteAllText(executable, string.Empty);

        var definition = ManualGameDefinition.Create("My Game", executable, null, "--safe-mode");

        Assert.Equal("My Game", definition.Title);
        Assert.Equal(Path.GetFullPath(_root), definition.WorkingDirectory);
        Assert.Equal(Path.GetFullPath(_root), definition.InstallRootPath);
        Assert.Equal("--safe-mode", definition.LaunchArguments);
    }

    [Fact]
    public void Valid_definition_preserves_a_distinct_install_root()
    {
        Directory.CreateDirectory(_root);
        var retail = Directory.CreateDirectory(Path.Combine(_root, "Retail")).FullName;
        var executable = Path.Combine(retail, "Game.exe");
        File.WriteAllText(executable, string.Empty);

        var definition = ManualGameDefinition.Create("Game", executable, retail, null, _root);

        Assert.Equal(Path.GetFullPath(retail), definition.WorkingDirectory);
        Assert.Equal(Path.GetFullPath(_root), definition.InstallRootPath);
    }

    [Fact]
    public void Invalid_executable_is_rejected()
    {
        Directory.CreateDirectory(_root);
        var text = Path.Combine(_root, "game.txt");
        File.WriteAllText(text, string.Empty);

        Assert.Throws<ArgumentException>(() => ManualGameDefinition.Create("Game", text, null));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
