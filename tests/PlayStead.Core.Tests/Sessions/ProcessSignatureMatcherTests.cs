using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.Sessions;

public sealed class ProcessSignatureMatcherTests
{
    private static readonly Guid GameId =
        Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly DateTimeOffset T0 =
        new(2026, 9, 12, 21, 0, 0, TimeSpan.Zero);

    private readonly ProcessSignatureMatcher _sut = new();

    [Fact]
    public void Main_executable_creates_a_main_match()
    {
        var signature = Signature(
            new ProcessSignatureEntry(
                "Game.exe",
                ProcessSignatureEntryKind.Main));

        var process = Snapshot(
            100,
            "game.EXE",
            @"C:\Games\Example\game.EXE");

        var result = _sut.Match(signature, [process]);

        Assert.True(result.HasMainProcess);
        Assert.Single(result.MainProcesses);
        Assert.Same(process, result.MainProcesses[0]);
        Assert.Empty(result.AuxiliaryProcesses);
        Assert.Empty(result.ExcludedProcesses);
    }

    [Fact]
    public void Auxiliary_only_never_counts_as_a_main_process()
    {
        var signature = Signature(
            new ProcessSignatureEntry(
                "Launcher.exe",
                ProcessSignatureEntryKind.Auxiliary));

        var process = Snapshot(
            101,
            "Launcher.exe",
            @"C:\Games\Example\Launcher.exe");

        var result = _sut.Match(signature, [process]);

        Assert.False(result.HasMainProcess);
        Assert.Empty(result.MainProcesses);
        Assert.Single(result.AuxiliaryProcesses);
        Assert.Same(process, result.AuxiliaryProcesses[0]);
        Assert.Empty(result.ExcludedProcesses);
    }

    [Fact]
    public void Excluded_entry_wins_over_main_for_the_same_executable()
    {
        var signature = Signature(
            new ProcessSignatureEntry(
                "Server.exe",
                ProcessSignatureEntryKind.Main),
            new ProcessSignatureEntry(
                "Server.exe",
                ProcessSignatureEntryKind.Excluded));

        var process = Snapshot(
            102,
            "SERVER.EXE",
            @"C:\Games\Example\SERVER.EXE");

        var result = _sut.Match(signature, [process]);

        Assert.False(result.HasMainProcess);
        Assert.Empty(result.MainProcesses);
        Assert.Empty(result.AuxiliaryProcesses);
        Assert.Single(result.ExcludedProcesses);
        Assert.Same(process, result.ExcludedProcesses[0]);
    }

    [Fact]
    public void Main_entry_wins_over_auxiliary_for_the_same_executable()
    {
        var signature = Signature(
            new ProcessSignatureEntry(
                "Game.exe",
                ProcessSignatureEntryKind.Auxiliary),
            new ProcessSignatureEntry(
                "Game.exe",
                ProcessSignatureEntryKind.Main));

        var process = Snapshot(
            103,
            "GAME.exe",
            @"C:\Games\Example\GAME.exe");

        var result = _sut.Match(signature, [process]);

        Assert.True(result.HasMainProcess);
        Assert.Single(result.MainProcesses);
        Assert.Empty(result.AuxiliaryProcesses);
        Assert.Empty(result.ExcludedProcesses);
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var signature = Signature(
            new ProcessSignatureEntry(
                "Game-Win64-Shipping.exe",
                ProcessSignatureEntryKind.Main));

        var process = Snapshot(
            104,
            "GAME-WIN64-SHIPPING.EXE",
            @"C:\Games\Example\GAME-WIN64-SHIPPING.EXE");

        var result = _sut.Match(signature, [process]);

        Assert.True(result.HasMainProcess);
        Assert.Single(result.MainProcesses);
    }

    [Fact]
    public void Multiple_main_executables_are_supported()
    {
        var signature = Signature(
            new ProcessSignatureEntry(
                "Game.exe",
                ProcessSignatureEntryKind.Main),
            new ProcessSignatureEntry(
                "Game-Win64-Shipping.exe",
                ProcessSignatureEntryKind.Main));

        var first = Snapshot(
            105,
            "Game.exe",
            @"C:\Games\Example\Game.exe");

        var second = Snapshot(
            106,
            "Game-Win64-Shipping.exe",
            @"C:\Games\Example\Game-Win64-Shipping.exe");

        var result = _sut.Match(signature, [first, second]);

        Assert.True(result.HasMainProcess);
        Assert.Equal(2, result.MainProcesses.Count);
        Assert.Contains(first, result.MainProcesses);
        Assert.Contains(second, result.MainProcesses);
    }

    [Fact]
    public void Unrelated_processes_are_ignored()
    {
        var signature = Signature(
            new ProcessSignatureEntry(
                "Game.exe",
                ProcessSignatureEntryKind.Main));

        var unrelated = Snapshot(
            107,
            "Notepad.exe",
            @"C:\Windows\System32\Notepad.exe");

        var result = _sut.Match(signature, [unrelated]);

        Assert.False(result.HasMainProcess);
        Assert.Empty(result.MainProcesses);
        Assert.Empty(result.AuxiliaryProcesses);
        Assert.Empty(result.ExcludedProcesses);
    }

    [Fact]
    public void Signature_without_main_entries_cannot_report_a_main_process()
    {
        var signature = Signature(
            new ProcessSignatureEntry(
                "Launcher.exe",
                ProcessSignatureEntryKind.Auxiliary),
            new ProcessSignatureEntry(
                "CrashReportClient.exe",
                ProcessSignatureEntryKind.Excluded));

        var launcher = Snapshot(
            108,
            "Launcher.exe",
            @"C:\Games\Example\Launcher.exe");

        var crashReporter = Snapshot(
            109,
            "CrashReportClient.exe",
            @"C:\Games\Example\CrashReportClient.exe");

        var result = _sut.Match(signature, [launcher, crashReporter]);

        Assert.False(result.HasMainProcess);
        Assert.Empty(result.MainProcesses);
        Assert.Single(result.AuxiliaryProcesses);
        Assert.Single(result.ExcludedProcesses);
    }

    private static ProcessSignature Signature(
        params ProcessSignatureEntry[] entries)
    {
        return new ProcessSignature(
            GameId,
            entries,
            ProcessSignatureOrigin.Manual,
            T0);
    }

    private static ProcessSnapshot Snapshot(
        int processId,
        string executableName,
        string executablePath)
    {
        return new ProcessSnapshot(
            processId,
            executableName,
            executablePath,
            T0);
    }
}
