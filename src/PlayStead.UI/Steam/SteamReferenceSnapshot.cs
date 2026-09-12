namespace PlayStead.UI.Steam;

public sealed record SteamReferenceSnapshot(
    IReadOnlyList<SteamReferenceEntry> Entries)
{
    public static SteamReferenceSnapshot Empty { get; } =
        new(Array.Empty<SteamReferenceEntry>());
}
