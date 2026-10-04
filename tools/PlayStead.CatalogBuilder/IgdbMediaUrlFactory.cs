namespace PlayStead.CatalogBuilder;

public static class IgdbMediaUrlFactory
{
    public static string? Cover(string? imageId) => Build("t_cover_big", imageId);

    public static string? Hero(string? imageId) => Build("t_1080p", imageId);

    private static string? Build(string transform, string? imageId) =>
        string.IsNullOrWhiteSpace(imageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/{transform}/{imageId.Trim()}.jpg";
}
