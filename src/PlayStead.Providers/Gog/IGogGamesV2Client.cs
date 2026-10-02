namespace PlayStead.Providers.Gog;

public interface IGogGamesV2Client
{
    Task<GogGamesV2Details?> GetAsync(string productId, CancellationToken cancellationToken);
}
