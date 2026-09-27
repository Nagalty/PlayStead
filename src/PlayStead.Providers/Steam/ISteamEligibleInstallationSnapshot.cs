namespace PlayStead.Providers.Steam;

/// <summary>
/// Carries the eligible installation set produced by the completed local Steam scan.
/// </summary>
public interface ISteamEligibleInstallationSnapshot
{
    IReadOnlySet<string>? EligibleExternalIds { get; }
}
