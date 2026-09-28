using System.IO.Compression;
using System.Text.Json;
using PlayStead.Core.Library;

namespace PlayStead.Core.LocalArtifacts;

public sealed record LocalArtifactRestoreResult(bool Succeeded, string Message, LocalArtifactSnapshot? SafetySnapshot = null)
{
    public static LocalArtifactRestoreResult Failure(string message) => new(false, message);
}

public interface ILocalArtifactRestoreService
{
    bool IsGameRunning(GameId gameId);
    Task<LocalArtifactRestoreResult> RestoreAsync(
        GameLocalArtifact artifact,
        LocalArtifactSnapshot snapshot,
        CancellationToken cancellationToken);
}

/// Restores a complete snapshot through a temporary extraction and a directory swap.
public sealed class LocalArtifactRestoreService(
    IArtifactFingerprintService fingerprintService,
    ILocalArtifactSnapshotService snapshotService,
    Func<GameId, bool>? isGameRunning = null) : ILocalArtifactRestoreService
{
    public bool IsGameRunning(GameId gameId) => isGameRunning?.Invoke(gameId) == true;

    public async Task<LocalArtifactRestoreResult> RestoreAsync(GameLocalArtifact artifact, LocalArtifactSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (artifact is null || snapshot is null || !artifact.Exists || !artifact.HasBaseline || !snapshot.IsValid)
            return LocalArtifactRestoreResult.Failure("La sauvegarde sélectionnée est indisponible.");
        if (IsGameRunning(artifact.GameId))
            return LocalArtifactRestoreResult.Failure("Fermez le jeu avant de restaurer cette sauvegarde.");
        if (!File.Exists(snapshot.ArchivePath))
            return LocalArtifactRestoreResult.Failure("La sauvegarde sélectionnée est introuvable.");

        LocalArtifactSnapshot safety;
        try
        {
            safety = await snapshotService.CreateAsync(artifact, cancellationToken, SnapshotReason.PreRestore).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return LocalArtifactRestoreResult.Failure("La sauvegarde de sécurité n’a pas pu être créée.");
        }

        var destination = Path.GetFullPath(artifact.Path);
        var parent = Directory.Exists(destination) ? Directory.GetParent(destination)!.FullName : Path.GetDirectoryName(destination)!;
        var temp = Path.Combine(parent, ".playstead-restore-" + Guid.NewGuid().ToString("N"));
        var backup = temp + ".backup";
        var replaced = false;
        try
        {
            ExtractSafely(snapshot.ArchivePath, temp, artifact.Path);
            var restoredPath = File.Exists(artifact.Path) ? Path.Combine(temp, Path.GetFileName(artifact.Path)) : temp;
            var restored = new GameLocalArtifact(artifact.GameId, artifact.Kind, restoredPath, artifact.Source, GameLocalArtifactStatus.KnownAndExists, artifact.RuleIdentity);
            var extractedFingerprint = await fingerprintService.ComputeAsync(restored, cancellationToken).ConfigureAwait(false);
            if (!extractedFingerprint.IsAvailable || !string.Equals(extractedFingerprint.Fingerprint!.Hash, snapshot.FingerprintHash, StringComparison.OrdinalIgnoreCase))
                return await FailAndKeepAsync(temp, backup, safety, "La vérification de la sauvegarde a échoué.").ConfigureAwait(false);

            if (File.Exists(destination))
            {
                File.Move(destination, backup);
                File.Move(Path.Combine(temp, Path.GetFileName(destination)), destination);
                replaced = true;
            }
            else
            {
                if (Directory.Exists(destination)) Directory.Move(destination, backup);
                Directory.Move(temp, destination);
                replaced = true;
            }

            var finalFingerprint = await fingerprintService.ComputeAsync(artifact, cancellationToken).ConfigureAwait(false);
            if (!finalFingerprint.IsAvailable || !string.Equals(finalFingerprint.Fingerprint!.Hash, snapshot.FingerprintHash, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(destination)) File.Delete(destination);
                else if (Directory.Exists(destination)) Directory.Delete(destination, true);
                if (File.Exists(backup)) File.Move(backup, destination);
                else if (Directory.Exists(backup)) Directory.Move(backup, destination);
                DeleteTemporary(temp);
                return LocalArtifactRestoreResult.Failure("La restauration a échoué. L’état précédent a été conservé.");
            }
            DeleteTemporary(backup);
            DeleteTemporary(temp);
            return new LocalArtifactRestoreResult(true, "Restauration terminée", safety);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            if (replaced)
            {
                DeleteTemporary(destination);
                if (File.Exists(backup)) File.Move(backup, destination);
                else if (Directory.Exists(backup)) Directory.Move(backup, destination);
            }
            DeleteTemporary(temp);
            DeleteTemporary(backup);
            return LocalArtifactRestoreResult.Failure("La restauration a échoué. L’état précédent a été conservé.");
        }
    }

    private static async Task<LocalArtifactRestoreResult> FailAndKeepAsync(string temp, string backup, LocalArtifactSnapshot safety, string message)
    {
        DeleteTemporary(temp);
        DeleteTemporary(backup);
        await Task.CompletedTask;
        return LocalArtifactRestoreResult.Failure(message);
    }

    private static void ExtractSafely(string archivePath, string temp, string originalPath)
    {
        Directory.CreateDirectory(temp);
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries.Where(e => !string.Equals(e.FullName, "metadata.json", StringComparison.OrdinalIgnoreCase)))
        {
            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar).Any(p => p == ".."))
                throw new InvalidDataException("Unsafe archive path.");
            var target = Path.GetFullPath(Path.Combine(temp, relative));
            if (!target.StartsWith(Path.GetFullPath(temp).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unsafe archive path.");
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: false);
        }
        if (File.Exists(originalPath))
        {
            var file = Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories).SingleOrDefault();
            if (file is null) throw new InvalidDataException("Snapshot contains no file.");
        }
    }

    private static void DeleteTemporary(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        else if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
