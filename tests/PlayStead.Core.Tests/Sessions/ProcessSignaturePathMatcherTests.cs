using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Tests.Sessions;

public sealed class ProcessSignaturePathMatcherTests
{
    internal static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;
    internal const string Path = @"C:\Games\One\Game.exe";
    internal static ProcessSignature Signature() => new(Guid.NewGuid(),
        [new("Game.exe", ProcessSignatureEntryKind.Main, Path, new FileRevision(10, T0))],
        ProcessSignatureOrigin.Discovered, T0,
        new(new InstallationId(Guid.NewGuid()), Guid.NewGuid(), 1,
            ProcessSignatureValidationState.Valid, Guid.NewGuid()));

    [Fact]
    public void Same_name_in_other_directory_does_not_match()
    {
        var result = new ProcessSignatureMatcher().Match(Signature(),
            [new(12, "Game.exe", @"C:\Games\Two\Game.exe", T0)]);
        Assert.False(result.HasMainProcess);
    }

    [Theory]
    [InlineData(Path, "Game.exe", true)]
    [InlineData(@"c:\games\one\GAME.EXE", "GAME.EXE", true)]
    [InlineData(@"C:\Games\Two\Game.exe", "Game.exe", false)]
    [InlineData(null, "Game.exe", false)]
    [InlineData("", "Game.exe", false)]
    [InlineData(" ", "Game.exe", false)]
    [InlineData(Path, "Other.exe", false)]
    public void Exact_path_and_name_match(string? path, string name, bool expected)
        => Assert.Equal(expected, Match(Signature(), path, name).HasMainProcess);

    [Theory]
    [InlineData(ProcessSignatureOrigin.Manual)]
    [InlineData(ProcessSignatureOrigin.BuiltIn)]
    public void Legacy_explicit_matches_name(ProcessSignatureOrigin origin)
        => Assert.True(Match(Signature() with { Origin = origin, Discovery = null,
            Entries = [new("Game.exe", ProcessSignatureEntryKind.Main)] }, null).HasMainProcess);

    [Theory]
    [InlineData(ProcessSignatureOrigin.Manual)]
    [InlineData(ProcessSignatureOrigin.BuiltIn)]
    public void Explicit_path_has_no_fallback(ProcessSignatureOrigin origin)
        => Assert.False(Match(Signature() with { Origin = origin, Discovery = null }, null).HasMainProcess);

    [Theory]
    [InlineData("legacy")]
    [InlineData("state")]
    [InlineData("unknown-state")]
    [InlineData("installation")]
    [InlineData("empty-installation")]
    [InlineData("generation")]
    [InlineData("empty-generation")]
    [InlineData("policy")]
    [InlineData("unknown-policy")]
    [InlineData("token")]
    [InlineData("path")]
    [InlineData("revision")]
    [InlineData("name")]
    [InlineData("traversal")]
    [InlineData("relative")]
    [InlineData("unknown-origin")]
    [InlineData("unknown-role")]
    public void Invalid_discovered_identity_is_rejected(string defect)
    {
        var signature = Malformed(Signature(), defect);
        var entry = signature.Entries[0];
        Assert.False(Match(signature, entry.ExecutablePath, entry.ExecutableName).HasMainProcess);
    }

    internal static ProcessSignature Malformed(ProcessSignature s, string defect) => defect switch
    {
        "legacy" => s with { Discovery = null },
        "state" => s with { Discovery = s.Discovery! with { ValidationState = ProcessSignatureValidationState.NeedsRevalidation } },
        "unknown-state" => s with { Discovery = s.Discovery! with { ValidationState = (ProcessSignatureValidationState)42 } },
        "installation" => s with { Discovery = s.Discovery! with { InstallationId = null } },
        "empty-installation" => s with { Discovery = s.Discovery! with { InstallationId = new InstallationId(Guid.Empty) } },
        "generation" => s with { Discovery = s.Discovery! with { GenerationId = null } },
        "empty-generation" => s with { Discovery = s.Discovery! with { GenerationId = Guid.Empty } },
        "policy" => s with { Discovery = s.Discovery! with { PolicyVersion = null } },
        "unknown-policy" => s with { Discovery = s.Discovery! with { PolicyVersion = 999 } },
        "token" => s with { Discovery = s.Discovery! with { ConcurrencyToken = Guid.Empty } },
        "path" => s with { Entries = [s.Entries[0] with { ExecutablePath = null }] },
        "revision" => s with { Entries = [s.Entries[0] with { ValidatedRevision = null }] },
        "name" => s with { Entries = [s.Entries[0] with { ExecutableName = "Wrong.exe" }] },
        "traversal" => s with { Entries = [s.Entries[0] with { ExecutablePath = @"C:\Games\One\..\One\Game.exe" }] },
        "relative" => s with { Entries = [s.Entries[0] with { ExecutablePath = @"One\Game.exe" }] },
        "unknown-origin" => s with { Origin = (ProcessSignatureOrigin)42 },
        "unknown-role" => s with { Entries = [s.Entries[0] with { Kind = (ProcessSignatureEntryKind)42 }] },
        _ => throw new ArgumentException(defect)
    };

    [Fact]
    public void Excluded_in_another_directory_does_not_hide_main()
    {
        var s = Signature();
        s = s with { Entries = [s.Entries[0], s.Entries[0] with {
            ExecutablePath = @"C:\Games\Two\Game.exe", Kind = ProcessSignatureEntryKind.Excluded }] };
        Assert.True(Match(s, Path).HasMainProcess);
    }

    [Theory]
    [InlineData(ProcessSignatureEntryKind.Excluded)]
    [InlineData(ProcessSignatureEntryKind.Main)]
    public void Role_precedence_applies_to_matching_entries(ProcessSignatureEntryKind highest)
    {
        var s = Signature();
        s = s with { Entries = [s.Entries[0] with { Kind = ProcessSignatureEntryKind.Auxiliary },
            s.Entries[0] with { Kind = highest }] };
        var result = Match(s, Path);
        Assert.Empty(result.AuxiliaryProcesses);
        Assert.Equal(highest == ProcessSignatureEntryKind.Main, result.HasMainProcess);
        Assert.Equal(highest == ProcessSignatureEntryKind.Excluded ? 1 : 0, result.ExcludedProcesses.Count);
    }

    private static ProcessSignatureMatch Match(ProcessSignature signature, string? path, string name = "Game.exe")
        => new ProcessSignatureMatcher().Match(signature, [new(12, name, path, T0)]);
}
