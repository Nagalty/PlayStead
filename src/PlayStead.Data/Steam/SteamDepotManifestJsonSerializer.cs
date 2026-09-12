using System.Text;
using System.Text.Json;

namespace PlayStead.Data.Steam;

public sealed class SteamDepotManifestJsonSerializer
{
    public string Serialize(
        IReadOnlyDictionary<string, string> depotManifestIds)
    {
        ArgumentNullException.ThrowIfNull(depotManifestIds);

        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Indented = false
                   }))
        {
            writer.WriteStartObject();

            foreach (var pair in depotManifestIds
                         .OrderBy(
                             x => x.Key,
                             StringComparer.Ordinal))
            {
                writer.WriteString(
                    pair.Key,
                    pair.Value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(
            stream.ToArray());
    }

    public IReadOnlyDictionary<string, string> Deserialize(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException(
                "Steam depot manifest JSON must be an object.");
        }

        var result = new SortedDictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new FormatException(
                    $"Steam depot manifest '{property.Name}' must be a string.");
            }

            var manifestId = property.Value.GetString();

            if (string.IsNullOrWhiteSpace(manifestId))
            {
                throw new FormatException(
                    $"Steam depot manifest '{property.Name}' is empty.");
            }

            result[property.Name] = manifestId;
        }

        return new Dictionary<string, string>(
            result,
            StringComparer.Ordinal);
    }
}
