namespace PlayStead.Core.Configuration;

public sealed class ConfigSemanticService : IConfigSemanticService
{
    private readonly IConfigDocumentAdapterResolver _resolver;
    private readonly IConfigBackupWriter _backupWriter;
    private readonly IConfigAtomicWriter _atomicWriter;
    private readonly IConfigKeyPolicy _policy;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Current, string Reference), (string Current, string Reference)> _comparisons = new();

    private static string Fingerprint(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    public ConfigSemanticService(IConfigDocumentAdapterResolver resolver, IConfigBackupWriter backupWriter, IConfigAtomicWriter atomicWriter, IConfigKeyPolicy? policy = null)
    {
        _resolver = resolver; _backupWriter = backupWriter; _atomicWriter = atomicWriter; _policy = policy ?? new EmptyConfigKeyPolicy();
    }

    public async Task<IReadOnlyList<ConfigSemanticChange>> CompareConfigAsync(string currentPath, string referencePath, CancellationToken cancellationToken)
    {
        var currentText = await File.ReadAllTextAsync(currentPath, cancellationToken);
        var referenceText = await File.ReadAllTextAsync(referencePath, cancellationToken);
        var adapter = _resolver.Resolve(currentPath, currentText) ?? throw new NotSupportedException($"No semantic adapter for '{currentPath}'.");
        var reference = adapter.Parse(referencePath, referenceText);
        var current = adapter.Parse(currentPath, currentText);
        var changes = adapter.Compare(reference, current, _policy);
        _comparisons[(Path.GetFullPath(currentPath), Path.GetFullPath(referencePath))] = (Fingerprint(currentText), Fingerprint(referenceText));
        return changes;
    }

    public async Task<ConfigRestoreResult> RestoreConfigValuesAsync(string currentPath, string referencePath, IReadOnlyCollection<string> selectedChangeIds, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        string? backup = null;
        try
        {
            var currentText = await File.ReadAllTextAsync(currentPath, cancellationToken);
            var currentFingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(currentText)));
            var referenceText = await File.ReadAllTextAsync(referencePath, cancellationToken);
            var adapter = _resolver.Resolve(currentPath, currentText);
            if (adapter is null) return new(false, null, [], "Format non supporté pour une restauration sémantique.");
            if (!_comparisons.TryGetValue((Path.GetFullPath(currentPath), Path.GetFullPath(referencePath)), out var compared) || compared.Current != Fingerprint(currentText) || compared.Reference != Fingerprint(referenceText))
                return new(false, null, [], "Le fichier a changé depuis la comparaison. Recharge les changements avant de restaurer.");
            var current = adapter.Parse(currentPath, currentText);
            var reference = adapter.Parse(referencePath, referenceText);
            var changes = adapter.Compare(reference, current, _policy);
            var selected = changes.Where(x => selectedChangeIds.Contains(x.Path, StringComparer.Ordinal) || selectedChangeIds.Contains(x.TechnicalPath, StringComparer.Ordinal)).ToArray();
            if (selected.Length == 0 || selected.Length != selectedChangeIds.Distinct(StringComparer.Ordinal).Count()) return new(false, null, [], "Sélection invalide. Recharge les changements.");
            if (selected.Any(x => !x.CanRestore)) return new(false, null, [], "Un ou plusieurs réglages sélectionnés ne sont pas restaurables.");
            var latestText = await File.ReadAllTextAsync(currentPath, cancellationToken);
            var latestFingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(latestText)));
            if (!string.Equals(currentFingerprint, latestFingerprint, StringComparison.Ordinal))
                return new(false, null, [], "Le fichier actuel a changé depuis la comparaison. Rouvre les changements.");
            backup = await _backupWriter.CreateBackupAsync(currentPath, cancellationToken);
            if (!File.Exists(backup) || Fingerprint(await File.ReadAllTextAsync(backup, cancellationToken)) != currentFingerprint)
                return new(false, backup, [], "La copie de sécurité n’a pas pu être vérifiée. Aucun réglage modifié.");
            var updated = adapter.ApplyChanges(current, selected);
            var serialized = adapter.Serialize(updated);
            adapter.Parse(currentPath, serialized);
            if (Fingerprint(await File.ReadAllTextAsync(currentPath, cancellationToken)) != currentFingerprint)
                return new(false, backup, [], "Le fichier a changé pendant la sauvegarde. Recharge les changements.");
            await _atomicWriter.WriteAsync(currentPath, serialized, cancellationToken);
            var verified = adapter.Parse(currentPath, await File.ReadAllTextAsync(currentPath, cancellationToken));
            var expected = adapter.Parse(currentPath, serialized);
            if (adapter.Compare(expected, verified, _policy).Count != 0)
                return new(false, backup, [], "La vérification de restauration a échoué.");
            return new(true, backup, selected, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or NotSupportedException or System.Text.Json.JsonException)
        {
            return new(false, backup, [], ex.Message);
        }
        finally { _gate.Release(); }
    }
}

public sealed class FileConfigBackupWriter : IConfigBackupWriter
{
    public async Task<string> CreateBackupAsync(string currentPath, CancellationToken cancellationToken)
    {
        var backupPath = currentPath + $".pre-restore-{Guid.NewGuid():N}.bak";
        await using var source = File.OpenRead(currentPath);
        await using var target = File.Create(backupPath);
        await source.CopyToAsync(target, cancellationToken);
        return backupPath;
    }
}

public sealed class AtomicConfigFileWriter : IConfigAtomicWriter
{
    public async Task WriteAsync(string path, string content, CancellationToken cancellationToken)
    {
        var tempPath = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(tempPath, content, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, path, true);
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }
}
