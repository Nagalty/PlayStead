using System.Text;
using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;

namespace PlayStead.Core.Tests.LocalArtifacts;

public sealed class LocalArtifactFingerprintServiceTests
{
    [Fact]
    public async Task File_fingerprint_is_stable_and_changes_when_content_changes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"playstead-fingerprint-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "alpha", Encoding.UTF8);
        try
        {
            var service = new Sha256ArtifactFingerprintService();
            var artifact = Artifact(path);
            var first = await service.ComputeAsync(artifact, CancellationToken.None);
            var same = await service.ComputeAsync(artifact, CancellationToken.None);
            await File.WriteAllTextAsync(path, "beta", Encoding.UTF8);
            var changed = await service.ComputeAsync(artifact, CancellationToken.None);

            Assert.True(first.IsAvailable);
            Assert.Equal(first.Fingerprint!.Hash, same.Fingerprint!.Hash);
            Assert.NotEqual(first.Fingerprint.Hash, changed.Fingerprint!.Hash);
            Assert.Equal("SHA256", first.Fingerprint.Algorithm);
            Assert.Equal(1, first.Fingerprint.FileCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Directory_fingerprint_is_order_independent_and_tracks_add_remove()
    {
        var root = Path.Combine(Path.GetTempPath(), $"playstead-fingerprint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "b"));
        Directory.CreateDirectory(Path.Combine(root, "a"));
        await File.WriteAllTextAsync(Path.Combine(root, "b", "two.txt"), "two");
        await File.WriteAllTextAsync(Path.Combine(root, "a", "one.txt"), "one");
        try
        {
            var service = new Sha256ArtifactFingerprintService();
            var artifact = Artifact(root);
            var first = await service.ComputeAsync(artifact, CancellationToken.None);
            File.Move(Path.Combine(root, "a", "one.txt"), Path.Combine(root, "a", "renamed.txt"));
            var renamed = await service.ComputeAsync(artifact, CancellationToken.None);
            File.Delete(Path.Combine(root, "a", "renamed.txt"));
            await File.WriteAllTextAsync(Path.Combine(root, "a", "one.txt"), "one");
            var restored = await service.ComputeAsync(artifact, CancellationToken.None);

            Assert.Equal(first.Fingerprint!.Hash, restored.Fingerprint!.Hash);
            Assert.NotEqual(first.Fingerprint.Hash, renamed.Fingerprint!.Hash);
            Assert.Equal(2, first.Fingerprint.FileCount);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Missing_or_invalid_artifact_is_unavailable()
    {
        var service = new Sha256ArtifactFingerprintService();
        var missing = await service.ComputeAsync(Artifact(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))), CancellationToken.None);
        var invalid = await service.ComputeAsync(Artifact("relative\\path"), CancellationToken.None);

        Assert.False(missing.IsAvailable);
        Assert.False(invalid.IsAvailable);
    }

    private static GameLocalArtifact Artifact(string path) =>
        new(GameId.New(), GameLocalArtifactKind.Configuration, path, GameLocalArtifactSource.ExplicitRule, GameLocalArtifactStatus.KnownAndExists);
}
