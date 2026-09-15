using PlayStead.Core.Library;
using PlayStead.Core.Sessions;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Tests.Sessions;

public sealed class ProcessSignatureValidationTests
{
    [Fact]
    public void Legacy_entry_defaults_to_no_path_or_revision()
    {
        var entry = new ProcessSignatureEntry("Game.exe", ProcessSignatureEntryKind.Main);

        Assert.Null(entry.ExecutablePath);
        Assert.Null(entry.ValidatedRevision);
    }

    [Fact]
    public void Legacy_signature_constructor_remains_compatible()
    {
        var signature = new ProcessSignature(Guid.NewGuid(), [], ProcessSignatureOrigin.Discovered, DateTimeOffset.UtcNow);

        Assert.Null(signature.Discovery);
    }

    [Fact]
    public void Discovery_metadata_retains_scope_generation_policy_and_token()
    {
        var installationId = new InstallationId(Guid.Parse("11111111-1111-4111-8111-111111111111"));
        var generationId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var concurrencyToken = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var metadata = new DiscoveredSignatureMetadata(
            installationId,
            generationId,
            4,
            ProcessSignatureValidationState.Valid,
            concurrencyToken);

        Assert.Equal(installationId, metadata.InstallationId);
        Assert.Equal(generationId, metadata.GenerationId);
        Assert.Equal(4, metadata.PolicyVersion);
        Assert.Equal(ProcessSignatureValidationState.Valid, metadata.ValidationState);
        Assert.Equal(concurrencyToken, metadata.ConcurrencyToken);
    }

    [Fact]
    public void Validation_state_has_safe_zero_default()
    {
        Assert.Equal(0, (int)ProcessSignatureValidationState.NeedsRevalidation);
        Assert.Equal(1, (int)ProcessSignatureValidationState.Valid);
    }

    [Fact]
    public void Path_and_revision_are_entry_specific()
    {
        var revision = new FileRevision(12, new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero));
        var first = new ProcessSignatureEntry("Game.exe", ProcessSignatureEntryKind.Main, @"C:\Games\Game.exe", revision);
        var second = new ProcessSignatureEntry("Helper.exe", ProcessSignatureEntryKind.Auxiliary);
        var signature = new ProcessSignature(Guid.NewGuid(), [first, second], ProcessSignatureOrigin.Discovered, DateTimeOffset.UtcNow);

        Assert.Equal(@"C:\Games\Game.exe", signature.Entries[0].ExecutablePath);
        Assert.Equal(revision, signature.Entries[0].ValidatedRevision);
        Assert.Null(signature.Entries[1].ExecutablePath);
        Assert.Null(signature.Entries[1].ValidatedRevision);
    }

    [Fact]
    public void Explicit_entries_do_not_require_discovery_metadata()
    {
        var manual = new ProcessSignature(Guid.NewGuid(), [new ProcessSignatureEntry("Manual.exe", ProcessSignatureEntryKind.Main)], ProcessSignatureOrigin.Manual, DateTimeOffset.UtcNow);
        var builtIn = new ProcessSignature(Guid.NewGuid(), [new ProcessSignatureEntry("BuiltIn.exe", ProcessSignatureEntryKind.Main)], ProcessSignatureOrigin.BuiltIn, DateTimeOffset.UtcNow);

        Assert.Null(manual.Discovery);
        Assert.Null(builtIn.Discovery);
    }
}
