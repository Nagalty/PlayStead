namespace PlayStead.UI.Library;

internal static class GenreDisplayLocalizer
{
    private static readonly IReadOnlyDictionary<string, string> French =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Adventure"] = "Aventure",
            ["Shooter"] = "Tir",
            ["SF"] = "Science-fiction",
            ["Fantasy"] = "Fantastique",
            ["Action"] = "Action",
            ["RPG"] = "RPG",
            ["Simulation"] = "Simulation",
            ["Strategy"] = "Stratégie",
            ["Tactical"] = "Tactique",
            ["Turn-based strategy (TBS)"] = "Stratégie au tour par tour",
            ["Sports"] = "Sports",
            ["Racing"] = "Course",
            ["Casual"] = "Occasionnel",
            ["Indie"] = "Indépendant",
            ["Massively Multiplayer"] = "Massivement multijoueur",
            ["Early Access"] = "Accès anticipé",
            ["Free to Play"] = "Gratuit",
            ["Survival"] = "Survie",
            ["Horror"] = "Horreur",
            ["Platformer"] = "Plateforme",
            ["Puzzle"] = "Réflexion"
        };

    public static string Localize(string genre)
    {
        ArgumentNullException.ThrowIfNull(genre);
        var value = genre.Trim();
        return value.Length == 0
            ? value
            : French.TryGetValue(value, out var translated)
                ? translated
                : value;
    }

    public static IReadOnlyList<string> LocalizeMany(IEnumerable<string> genres)
    {
        ArgumentNullException.ThrowIfNull(genres);
        var values = genres
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

        var hasFpp = values.Any(value => string.Equals(value, "FPP", StringComparison.OrdinalIgnoreCase));
        var hasTpp = values.Any(value => string.Equals(value, "TPP", StringComparison.OrdinalIgnoreCase));
        var hasShooter = values.Any(value =>
            string.Equals(value, "Tir", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "Shooter", StringComparison.OrdinalIgnoreCase));

        var result = new List<string>();
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (consumed.Contains(value))
                continue;

            if (hasFpp && hasShooter &&
                (string.Equals(value, "FPP", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(value, "Tir", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(value, "Shooter", StringComparison.OrdinalIgnoreCase)))
            {
                AddDistinct(result, "FPS");
                consumed.Add(value);
                continue;
            }

            if (hasTpp && hasShooter &&
                (string.Equals(value, "TPP", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(value, "Tir", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(value, "Shooter", StringComparison.OrdinalIgnoreCase)))
            {
                AddDistinct(result, "TPS");
                consumed.Add(value);
                continue;
            }

            AddDistinct(result, Localize(value));
        }

        return result;
    }

    private static void AddDistinct(ICollection<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
            values.Add(value);
    }
}
