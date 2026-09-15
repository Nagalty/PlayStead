using PlayStead.Platform.Processes.Discovery;

namespace PlayStead.Platform.Tests.Processes.Discovery;

public sealed class WindowsExecutablePathTests
{
    [Fact]
    public void Root_normalizes_separators_without_requiring_an_existing_directory()
    {
        Assert.Equal(@"C:\Games\Foo",
            WindowsExecutablePath.NormalizeRoot("C:/Games/Foo/"));
    }

    [Fact]
    public void Root_preserves_the_volume_separator()
    {
        Assert.Equal(@"C:\", WindowsExecutablePath.NormalizeRoot(@"C:\\"));
    }

    [Fact]
    public void Root_removes_trailing_separators_below_the_volume()
    {
        Assert.Equal(@"C:\Games\Foo",
            WindowsExecutablePath.NormalizeRoot(@"C:\Games\Foo\\"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Games/Foo")]
    [InlineData(@"\Games\Foo")]
    [InlineData(@"C:Games\Foo")]
    [InlineData(@"\\server\share\Foo")]
    [InlineData(@"\\?\C:\Games\Foo")]
    [InlineData(@"\\.\C:\Games\Foo")]
    [InlineData(@"C:\Games\..\Foo")]
    [InlineData(@"C:\Games\.\Foo")]
    [InlineData(@"C:\Games\Foo.")]
    [InlineData(@"C:\Games\Foo ")]
    [InlineData(@"C:\Games\Fo*o")]
    [InlineData(@"C:\Games\Fo?o")]
    [InlineData(@"C:\Games\Fo:o")]
    [InlineData(@"C:\Games\Fo|o")]
    [InlineData("C:\\Games\\Fo\0o")]
    public void Root_rejects_untrusted_windows_path_forms(string? root)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => WindowsExecutablePath.NormalizeRoot(root!));
    }

    [Theory]
    [InlineData(@"C:\Games\Foo\game.exe", true)]
    [InlineData(@"c:\games\foo\bin\GAME.EXE", true)]
    [InlineData("C:/Games/Foo/bin/game.exe", true)]
    [InlineData(@"C:\Games\FooBar\game.exe", false)]
    [InlineData(@"C:\Games\Foo", false)]
    [InlineData(@"C:\Games\Foo\..\FooBar\game.exe", false)]
    [InlineData(@"D:\Games\Foo\game.exe", false)]
    public void Boundary_is_a_directory_boundary(string candidate, bool expected)
    {
        Assert.Equal(expected,
            WindowsExecutablePath.IsStrictlyUnderRoot(@"C:\Games\Foo", candidate));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:Games\Foo\game.exe")]
    [InlineData(@"\\server\share\game.exe")]
    [InlineData(@"C:\Games\Foo\..\Foo\game.exe")]
    [InlineData(@"C:\Games\Foo\game.exe.")]
    [InlineData(@"C:\Games\Foo\game.exe ")]
    [InlineData(@"C:\Games\Foo\ga*me.exe")]
    [InlineData(@"C:\Games\Foo\ga:me.exe")]
    public void Boundary_rejects_invalid_candidates(string? candidate)
    {
        Assert.False(WindowsExecutablePath.IsStrictlyUnderRoot(
            @"C:\Games\Foo", candidate!));
    }

    [Fact]
    public void Boundary_rejects_an_invalid_root_as_an_argument_error()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            WindowsExecutablePath.IsStrictlyUnderRoot(
                @"C:\Games\..\Foo", @"C:\Foo\game.exe"));
    }

    [Fact]
    public void Volume_root_is_a_strict_ancestor_of_a_volume_file()
    {
        Assert.True(WindowsExecutablePath.IsStrictlyUnderRoot(
            @"C:\", @"C:\game.exe"));
    }
}
