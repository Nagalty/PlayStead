using System.Text.Json;
using System.Text.Json.Nodes;

namespace PlayStead.Core.Configuration;

public sealed class JsonConfigDocumentAdapter : IConfigDocumentAdapter
{
    public string Format => "JSON";
    public bool CanHandle(string path, string content) => string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase);

    public ConfigDocument Parse(string path, string content)
    {
        using var parsed = JsonDocument.Parse(content);
        return new ConfigDocument(Format, content, parsed.RootElement.Clone(), content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n", content.StartsWith('\uFEFF'));
    }

    public IReadOnlyList<ConfigSemanticChange> Compare(ConfigDocument reference, ConfigDocument current, IConfigKeyPolicy? policy = null)
    {
        var result = new List<ConfigSemanticChange>();
        CompareValue((JsonElement)reference.Value, (JsonElement)current.Value, string.Empty, result, policy ?? new EmptyConfigKeyPolicy());
        return result;
    }

    public ConfigDocument ApplyChanges(ConfigDocument current, IEnumerable<ConfigSemanticChange> selectedChanges)
    {
        var root = JsonNode.Parse(((JsonElement)current.Value).GetRawText()) ?? throw new FormatException("JSON root is null.");
        foreach (var change in selectedChanges.Where(x => x.CanRestore && x.ChangeKind == ConfigChangeKind.Modified))
        {
            var segments = change.TechnicalPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) continue;
            var parent = root;
            for (var i = 0; i < segments.Length - 1; i++) parent = parent[segments[i]] ?? throw new FormatException("JSON path is missing.");
            parent[segments[^1]] = JsonNode.Parse(change.OldValue ?? "null");
        }
        return current with { Value = JsonDocument.Parse(root.ToJsonString()).RootElement.Clone() };
    }

    public string Serialize(ConfigDocument document) => JsonSerializer.Serialize((JsonElement)document.Value, new JsonSerializerOptions { WriteIndented = true });

    private static void CompareValue(JsonElement oldValue, JsonElement currentValue, string path, List<ConfigSemanticChange> result, IConfigKeyPolicy policy)
    {
        if (oldValue.ValueKind == JsonValueKind.Object && currentValue.ValueKind == JsonValueKind.Object)
        {
            var oldProps = oldValue.EnumerateObject().ToDictionary(x => x.Name, StringComparer.Ordinal);
            var currentProps = currentValue.EnumerateObject().ToDictionary(x => x.Name, StringComparer.Ordinal);
            foreach (var name in oldProps.Keys.Union(currentProps.Keys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            {
                var childPath = string.IsNullOrEmpty(path) ? name : $"{path}.{name}";
                if (!oldProps.TryGetValue(name, out var oldChild)) result.Add(Change(childPath, null, currentProps[name].Value, ConfigChangeKind.Added, false, policy));
                else if (!currentProps.TryGetValue(name, out var currentChild)) result.Add(Change(childPath, oldChild.Value, null, ConfigChangeKind.Removed, false, policy));
                else
                {
                    var start = result.Count;
                    CompareValue(oldChild.Value, currentChild.Value, childPath, result, policy);
                    if (name.Contains('.') || name.Length == 0)
                        for (var i = start; i < result.Count; i++) result[i] = result[i] with { CanRestore = false };
                }
            }
            return;
        }
        if (oldValue.ValueKind == JsonValueKind.Array || currentValue.ValueKind == JsonValueKind.Array)
        {
            if (!oldValue.GetRawText().Equals(currentValue.GetRawText(), StringComparison.Ordinal)) result.Add(Change(path, oldValue, currentValue, ConfigChangeKind.Modified, false, policy));
            return;
        }
        if (!oldValue.GetRawText().Equals(currentValue.GetRawText(), StringComparison.Ordinal)) result.Add(Change(path, oldValue, currentValue, ConfigChangeKind.Modified, IsCompatiblePrimitive(oldValue, currentValue), policy));
    }

    private static bool IsCompatiblePrimitive(JsonElement oldValue, JsonElement currentValue) =>
        oldValue.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array) && currentValue.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array) &&
        (oldValue.ValueKind == currentValue.ValueKind ||
        (oldValue.ValueKind is JsonValueKind.True or JsonValueKind.False && currentValue.ValueKind is JsonValueKind.True or JsonValueKind.False));

    private static ConfigSemanticChange Change(string path, JsonElement? oldValue, JsonElement? currentValue, ConfigChangeKind kind, bool canRestore, IConfigKeyPolicy policy) => new(path, null, oldValue?.GetRawText(), currentValue?.GetRawText(), kind, canRestore && !policy.IsProtected(path), ConfigChangeConfidence.High, path);
}
