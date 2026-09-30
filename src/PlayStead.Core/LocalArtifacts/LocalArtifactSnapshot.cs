using System.IO.Compression;
using System.Text.Json;
using PlayStead.Core.Library;

namespace PlayStead.Core.LocalArtifacts;

public sealed record LocalArtifactSnapshot(
    Guid SnapshotId,
    GameId GameId,
    GameLocalArtifactKind ArtifactKind,
    string RuleIdentity,
    string ArchivePath,
    string FingerprintAlgorithm,
    string FingerprintHash,
    int FileCount,
    long TotalSizeBytes,
    DateTimeOffset CreatedAtUtc,
    bool IsValid = true,
    SnapshotReason Reason = SnapshotReason.Manual)
{
    public string StatusLabel => IsValid ? string.Empty : "Corrompu";
    public string ReasonLabel => Reason switch
    {
        SnapshotReason.PreRestore => "Sauvegarde de sécurité",
        SnapshotReason.InitialProtection => "Première sauvegarde de sécurité",
        SnapshotReason.PreUpdate => "Sauvegarde avant mise à jour",
        _ => string.Empty
    };
}

public enum SnapshotReason
{
    Manual = 0,
    PreRestore = 1,
    InitialProtection = 2,
    PreUpdate = 3
}

public interface ILocalArtifactSnapshotStore
{
    Task<IReadOnlyList<LocalArtifactSnapshot>> GetAsync(GameId gameId, GameLocalArtifactKind kind, string ruleIdentity, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocalArtifactSnapshot>> GetAllAsync(CancellationToken cancellationToken);
    Task UpsertAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken);
    Task DeleteAsync(Guid snapshotId, CancellationToken cancellationToken);
}

public interface ILocalArtifactSnapshotService
{
    Task<LocalArtifactSnapshot> CreateAsync(GameLocalArtifact artifact, CancellationToken cancellationToken, SnapshotReason reason = SnapshotReason.Manual);
    Task<IReadOnlyList<LocalArtifactSnapshot>> ListAsync(GameLocalArtifact artifact, CancellationToken cancellationToken);
    Task DeleteAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken);
}

public sealed class LocalArtifactSnapshotService(
    IArtifactFingerprintService fingerprintService,
    ILocalArtifactSnapshotStore store,
    string snapshotsRoot,
    ILocalSnapshotStorageService? storage = null) : ILocalArtifactSnapshotService
{
    public async Task<LocalArtifactSnapshot> CreateAsync(GameLocalArtifact artifact, CancellationToken cancellationToken, SnapshotReason reason = SnapshotReason.Manual)
    {
        if (artifact.Kind == GameLocalArtifactKind.Log || !artifact.Exists || !artifact.HasBaseline || string.IsNullOrWhiteSpace(artifact.RuleIdentity))
            throw new InvalidOperationException("Only an existing, identified artifact can be snapshotted.");

        var before = await fingerprintService.ComputeAsync(artifact, cancellationToken).ConfigureAwait(false);
        if (!before.IsAvailable)
            throw new IOException(before.Error ?? "Artifact fingerprint unavailable.");

        var id = Guid.NewGuid();
        var directory = Path.Combine(snapshotsRoot, artifact.GameId.Value.ToString("D"), artifact.Kind.ToString(), artifact.RuleIdentity);
        Directory.CreateDirectory(directory);
        var finalPath = Path.Combine(directory, id.ToString("N") + ".zip");
        var tempPath = finalPath + ".tmp";
        try
        {
            await using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
            {
                var metadata = new SnapshotMetadata(artifact.GameId, artifact.Kind, artifact.RuleIdentity!, artifact.Path, before.Fingerprint!);
                var metadataEntry = archive.CreateEntry("metadata.json", CompressionLevel.Fastest);
                await using (var metadataStream = metadataEntry.Open())
                    await JsonSerializer.SerializeAsync(metadataStream, metadata, cancellationToken: cancellationToken).ConfigureAwait(false);
                AddArtifact(archive, artifact.Path, cancellationToken);
            }

            var after = await fingerprintService.ComputeAsync(artifact, cancellationToken).ConfigureAwait(false);
            if (!after.IsAvailable || after.Fingerprint!.Hash != before.Fingerprint!.Hash)
                throw new IOException("Artifact changed while the snapshot was created.");
            var snapshot = new LocalArtifactSnapshot(id, artifact.GameId, artifact.Kind, artifact.RuleIdentity!, finalPath, before.Fingerprint.Algorithm, before.Fingerprint.Hash, before.Fingerprint.FileCount, before.Fingerprint.TotalSizeBytes, before.Fingerprint.CapturedAtUtc, true, reason);
            File.Move(tempPath, finalPath);
            if (storage is not null)
                await storage.EnsureCapacityForAsync(snapshot, cancellationToken).ConfigureAwait(false);
            await store.UpsertAsync(snapshot, cancellationToken).ConfigureAwait(false);
            return snapshot;
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }

    public async Task<IReadOnlyList<LocalArtifactSnapshot>> ListAsync(GameLocalArtifact artifact, CancellationToken cancellationToken)
    {
        if (artifact.RuleIdentity is null) return [];
        var snapshots = await store.GetAsync(artifact.GameId, artifact.Kind, artifact.RuleIdentity, cancellationToken).ConfigureAwait(false);
        return snapshots.Select(snapshot => snapshot with { IsValid = File.Exists(snapshot.ArchivePath) && IsReadable(snapshot.ArchivePath) }).ToArray();
    }

    public async Task DeleteAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (storage is not null)
            await storage.DeleteSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        else
        {
            if (File.Exists(snapshot.ArchivePath)) File.Delete(snapshot.ArchivePath);
            await store.DeleteAsync(snapshot.SnapshotId, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void AddArtifact(ZipArchive archive, string path, CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            archive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Fastest);
            return;
        }
        var root = Path.GetFullPath(path);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse points are not supported.");
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            archive.CreateEntryFromFile(file, relative, CompressionLevel.Fastest);
        }
    }

    private static bool IsReadable(string path)
    {
        try { using var archive = ZipFile.OpenRead(path); return archive.GetEntry("metadata.json") is not null; }
        catch (IOException) { return false; }
        catch (InvalidDataException) { return false; }
    }

    private sealed record SnapshotMetadata(GameId GameId, GameLocalArtifactKind ArtifactKind, string RuleIdentity, string OriginalPath, ArtifactFingerprint Fingerprint);
}
