using System.Text;

namespace PlayStead.Core.Configuration;

public sealed class IniConfigDocumentAdapter : IConfigDocumentAdapter
{
    public string Format => "INI";
    public bool CanHandle(string path, string content) => string.Equals(Path.GetExtension(path), ".ini", StringComparison.OrdinalIgnoreCase);

    public ConfigDocument Parse(string path, string content)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var section = string.Empty;
        foreach (var raw in content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1].Trim(); continue; }
            var separator = line.IndexOf('=');
            if (separator <= 0) throw new FormatException("Configuration INI invalide ou non prise en charge.");
            var key = line[..separator].Trim();
            if (!map.TryAdd($"{section}\u001f{key}", line[(separator + 1)..].Trim()))
                throw new FormatException("Les clés INI répétées ne permettent pas une restauration sélective sûre.");
        }
        return new ConfigDocument(Format, content, new IniValueMap(map), content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n", content.StartsWith('\uFEFF'));
    }

    public IReadOnlyList<ConfigSemanticChange> Compare(ConfigDocument reference, ConfigDocument current, IConfigKeyPolicy? policy = null)
    {
        var oldMap = ((IniValueMap)reference.Value).Values;
        var newMap = ((IniValueMap)current.Value).Values;
        var result = new List<ConfigSemanticChange>();
        foreach (var path in oldMap.Keys.Union(newMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            oldMap.TryGetValue(path, out var oldValue); newMap.TryGetValue(path, out var currentValue);
            if (string.Equals(oldValue, currentValue, StringComparison.Ordinal)) continue;
            var kind = !oldMap.ContainsKey(path) ? ConfigChangeKind.Added : !newMap.ContainsKey(path) ? ConfigChangeKind.Removed : ConfigChangeKind.Modified;
            var canRestore = kind == ConfigChangeKind.Modified && policy?.IsProtected(path) != true;
            result.Add(new ConfigSemanticChange(path.Replace('\u001f', '.'), null, oldValue, currentValue, kind, canRestore, ConfigChangeConfidence.High, path));
        }
        return result;
    }

    public ConfigDocument ApplyChanges(ConfigDocument current, IEnumerable<ConfigSemanticChange> selectedChanges)
    {
        var map = new Dictionary<string, string?>(((IniValueMap)current.Value).Values, StringComparer.OrdinalIgnoreCase);
        foreach (var change in selectedChanges.Where(x => x.CanRestore && x.ChangeKind == ConfigChangeKind.Modified))
            map[change.TechnicalPath] = change.OldValue;
        return current with { Value = new IniValueMap(map) };
    }

    public string Serialize(ConfigDocument document)
    {
        var map = ((IniValueMap)document.Value).Values;
        var section = string.Empty;
        var builder = new StringBuilder();
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(document.OriginalText, @"[^\r\n]*(?:\r\n|\r|\n|$)"))
        {
            var raw = match.Value;
            var line = raw.TrimEnd('\r', '\n');
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']')) section = trimmed[1..^1].Trim();
            var separator = line.IndexOf('=');
            if (separator > 0 && !trimmed.StartsWith(';') && !trimmed.StartsWith('#'))
            {
                var key = section + "\u001f" + line[..separator].Trim();
                if (map.TryGetValue(key, out var value) && line[(separator + 1)..].Trim() != value)
                {
                    builder.Append(line[..(separator + 1)]).Append(value).Append(raw[line.Length..]);
                    continue;
                }
            }
            builder.Append(raw);
        }
        return builder.ToString();
    }

    private sealed record IniValueMap(IReadOnlyDictionary<string, string?> Values);
}
