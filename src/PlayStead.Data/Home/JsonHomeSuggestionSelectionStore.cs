using System.Text.Json;
using PlayStead.Core.Home;
using PlayStead.Core.Library;

namespace PlayStead.Data.Home;

public sealed class JsonHomeSuggestionSelectionStore : IHomeSuggestionSelectionStore
{
    private readonly string _path;
    public JsonHomeSuggestionSelectionStore(string path) => _path = path;

    public async Task<GameId?> GetLastSuggestionGameIdAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return null;
        try
        {
            await using var stream = File.OpenRead(_path);
            var value = await JsonSerializer.DeserializeAsync<State>(stream, cancellationToken: cancellationToken);
            return value?.GameId is Guid id ? new GameId(id) : null;
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    public async Task SetLastSuggestionGameIdAsync(GameId? gameId, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, new State(gameId?.Value), cancellationToken: cancellationToken);
    }

    private sealed record State(Guid? GameId);
}
