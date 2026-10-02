namespace PlayStead.UI.Library;

internal static class GenreDisplayLocalizer
{
    private static readonly IReadOnlyDictionary<string, string> French =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Adventure"] = "Aventure",
            ["Shooter"] = "Tir",
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
        return genres
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Localize)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
